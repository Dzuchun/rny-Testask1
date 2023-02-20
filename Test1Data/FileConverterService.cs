using Microsoft.VisualBasic.FileIO;
using System.Collections;
using System.Configuration;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Timers;
using Test1Data.LineProcess;

namespace Test1Data
{
    public class FileConverterService
    {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        // No, I won't do that. They all get inited in start() and are private.
        private string _aPath, _bPath;
        private bool _started = false;
        private readonly object _startedLock = new();
        private FileSystemWatcher _watcher;
        private System.Timers.Timer _timer;

        private IFileProcessor _txtProcessor;
        private IFileProcessor _csvProcessor;

        private int _todayFileCount;
        private int _todayLineCount;
        private string _todayDirectoryName ;
        private int _todayBadLines;
        private readonly HashSet<string> _todayBadFileNames = new();
        private readonly object _todayInfoLock = new();
        // Here was a special class, that was supposed to allow Stop() and OnMidnight() operations delaying, so that all files could be processed before shutdown/meta.log creation
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        /// <summary>
        /// Initialises and starts the service.
        /// </summary>
        public void Start()
        {
            lock (_startedLock)
            {
                if (_started)
                {
                    return;
                }
                _started = true;
            }

            try
            {
                this.LoadPaths();
                this.SetupWatcher();
                this.SetupTimer();
                this.InjectProcessors();
                this.ResetDailyFields();

                // this might help to cut time on first parallel LINQ invoke
                Enumerable.Empty<object>().AsParallel().FirstOrDefault();
            }
            catch(Exception)
            {
                // something went wrong - stop the service.
                lock (_startedLock) _started= false;
                Stop();
                throw;
            }
        }

        /// <summary>
        /// Stops the service.
        /// </summary>
        public void Stop()
        {
            if (!_started) return;

            _aPath = null!;
            _bPath = null!;

            _watcher?.Dispose();
            _watcher = null!;

            _timer?.Dispose();
            _timer = null!;

            // Reset today fields
            this.ResetDailyFields();

            _started = false;
        }

        /// <summary>
        /// Restarts the service.
        /// </summary>
        public void Restart()
        {
            this.Stop();
            this.Start();
        }

        private void LoadPaths()
        {
            string? aPath = ConfigurationManager.AppSettings["a-path"];
            string? bPath = ConfigurationManager.AppSettings["b-path"];

            // check if configuration was setup correctly
            if (string.IsNullOrEmpty(aPath) || string.IsNullOrEmpty(bPath))
            {
                throw new FileNotFoundException("A or B folder paths were not found or empty.");
            }

            // check if specified directories exist
            if (!FileSystem.DirectoryExists(aPath))
            {
                throw new FileNotFoundException("Specified A folder does not exist", aPath);
            }
            if (!FileSystem.DirectoryExists(bPath))
            {
                throw new FileNotFoundException("Specified B folder does not exist", bPath);
            }

            // paths are ok, I guess
            this._aPath = aPath;
            this._bPath = bPath;
        }

        private void SetupWatcher()
        {
            // create and configure watcher
            _watcher = new()
            {
                Path = _aPath,
                NotifyFilter = NotifyFilters.CreationTime | NotifyFilters.FileName,
                EnableRaisingEvents = true,
                IncludeSubdirectories = false,

                // source: https://stackoverflow.com/a/61145821
                Filters = { "*.txt", "*.csv" },
            };


            _watcher.Created += this.OnFileCreation;
        }

        private void SetupTimer()
        {
            // since there is no "delay" arg for a timer in C#, I'm going to make a mess:

            // create timer to wait for midnight
            _timer = new()
            {
                // delay to midnight
                // wow, preprocessor commands work so bad in C#. I serfed through all guidelines I could find, and still have no clue, how to use them.
                // (DEBUG seem to ALWAYS be true. other constants are not seen properly too.)
//#if (DEBUG)
//                Interval = 30000,
//#else
                Interval = DateTime.Today.AddDays(1).Subtract(DateTime.Now).TotalMilliseconds,
//#endif
                AutoReset = true,
                Enabled= true,
            };

            // local function that swaps timer delay and event handler
            void swapInvoke(object? o, ElapsedEventArgs args)
            {
                // changing delay
//#if (DEBUG)
//                _timer.Interval = TimeSpan.FromMinutes(1).TotalMilliseconds;
//#elif (!DEBUG)
                _timer.Interval = TimeSpan.FromDays(1).TotalMilliseconds;
//#endif

                // swap handler
                _timer.Elapsed += OnMidnight;
                _timer.Elapsed -= swapInvoke;
                // invoke midnight
                OnMidnight(o, args);
            }
            _timer.Elapsed += swapInvoke;
            // this <s>trick</s> atrocious code allowes me to care about single timer.
        }

        private void InjectProcessors()
        {
            _txtProcessor = new TxtFileProcessor();
            _csvProcessor = new CsvFileProcessor();
        }

        private void ResetDailyFields()
        {
            // enter daily fields lock, so that no threads will use these fields.
            lock (_todayInfoLock)
            {
                this._todayFileCount = 0; // it gets incremented before setting output file name, so that's ok
                this._todayDirectoryName = $"{this._bPath}/{DateTime.Today:MM-dd-yyyy}";
                this._todayLineCount = 0;
                this._todayBadLines = 0;
                this._todayBadFileNames.Clear();
            }
        }

        private IFileProcessor GetProcessor(FileSystemEventArgs args)
        {
            if (args.Name is null)
            {
                throw new ArgumentException("File name cannot be null", nameof(args));
            }
            string ext = Path.GetExtension(args.Name).ToLowerInvariant();
            return ext switch
            {
                ".txt" => this._txtProcessor,
                ".csv" => this._csvProcessor,
                _ => throw new ArgumentException($"File extension is unknown: {ext}", nameof(args)),
            };
        }

        private async void OnFileCreation(object? sender, FileSystemEventArgs args)
        {
//#if DEBUG
            Console.WriteLine($"Started processing {args.Name}");
//#endif
           var sw = Stopwatch.StartNew();
            try
            {
                // safely create input stream, if course in async
                // max timeout here is 1000ms, it's kinda random though.
                using FileStream inStream = await Task.Run(() => GetFileStream($"{this._aPath}/{args.Name}", 1000, FileAccess.Read));
                // get file processor for it's type.
                IFileProcessor processor = this.GetProcessor(args);
                // process file in async, just in case
                (JsonArray result, int totalLines, int badLines) result = await Task.Run(() => processor.process(inStream));
                string outfile;
                // update today stats
                lock (_todayInfoLock)
                {
                    this._todayLineCount++;
                    this._todayFileCount++;
                    if (result.badLines != 0)
                    {
                        // current file has errors
                        this._todayBadLines += result.badLines;
                        this._todayBadFileNames.Add(args.FullPath);
                    }
                    // create today directory if it does not exist already
                    Directory.CreateDirectory(this._todayDirectoryName);
                    // create output file name
                    outfile = $"{this._todayDirectoryName}/output{this._todayFileCount}.json";
                    // may exit lock now, since I've already updated/used today variables
                }
                // obtain output file stream safely (some stress tests showed that it's not optional. at least on my system, that is)
                // of course it's async
                using FileStream outStream = await Task.Run(() => GetFileStream(outfile, 1000, FileAccess.Write));
                // serialize and write data to file, again in async
                await JsonSerializer.SerializeAsync<JsonArray>(outStream, result.result, new JsonSerializerOptions
                {
                    // this tells JsonSerialiser to write using fancy format thing
                    WriteIndented = true,
                });
                // some debug info
                sw.Stop();
//#if DEBUG
                Console.WriteLine($"Finished processing {args.Name} in {sw.Elapsed.TotalMilliseconds}ms, result saved to {outfile}");
//#endif
            }
            catch (Exception e)
            {
                sw.Stop();
//#if DEBUG
                Console.WriteLine($"{args.Name} file processing threw exception:\n{e.GetType().Name}\n{e.Message}");
//#endif
                throw;
            }
        }

        // source: https://stackoverflow.com/a/37154588
        // for some reason, C# would attempt to access file SO quickly, that copy-pasting existing files result in "file used by other process" kind of exception
        // wow.
        private static FileStream GetFileStream(string path, int timeoutMs, FileAccess access)
        {
            var time = Stopwatch.StartNew();
            while (time.ElapsedMilliseconds < timeoutMs)
            {
                try
                {
                    return new FileStream(path, FileMode.OpenOrCreate, access);
                }
                catch (IOException e) when (e.HResult == -2147024864) {/*catch and eat the exception, if that's a file access error*/}
            }

            throw new TimeoutException($"Failed to get a write handle to {path} within {timeoutMs}ms.");
        }

#pragma warning disable S1172 // Unused method parameters should be removed
        // These params are part of a delegate, and can't be removed.
        private void OnMidnight(object? sender, ElapsedEventArgs args)
#pragma warning restore S1172 // Unused method parameters should be removed
        {
            string metaFileName;
            int parsedFiles;
            int parsedLines;
            int foundErrors;
            string[] invalidFiles;
            // aquire lock, so that following data is consistent
            lock (_todayInfoLock)
            {
                metaFileName = $"{_todayDirectoryName}/meta.log";
                parsedFiles = this._todayFileCount;
                parsedLines = this._todayLineCount;
                foundErrors = this._todayBadLines;
                invalidFiles = this._todayBadFileNames.ToArray();
                // release the lock, since all required data is already saved localy
            }
            // obtain output file stream safely. I did not test code without this sort of workaround
            using FileStream outStream = GetFileStream(metaFileName, 1000, FileAccess.Write);
            // create file writer
            using StreamWriter sw = new(outStream);
            // write data
            sw.Write($"parsed_files: {parsedFiles}\nparsed_lines: {parsedLines}\nfound_errors: {foundErrors}\ninvalid_files: [{string.Join(", ", invalidFiles)}]\n");

//#if DEBUG
            Console.WriteLine($"metadata was written to {metaFileName}");
//#endif

            // reset daily
            this.ResetDailyFields();

        }
    }
}
