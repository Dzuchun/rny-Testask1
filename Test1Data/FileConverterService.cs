using Microsoft.VisualBasic.FileIO;
using System.Configuration;
using System.Timers;

namespace Test1Data
{
    public class FileConverterService
    {
        public FileConverterService() { }

        private string _aPath, _bPath;
        private bool _started = false;
        private readonly object _startedLock = new();
        private FileSystemWatcher _watcher;
        private System.Timers.Timer _timer;

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
            }
            catch(Exception)
            {
                // something went wrong - stop the service.
                lock (_startedLock) _started= false;
                Stop(true);
                throw;
            }
        }

        /// <summary>
        /// Stops the service
        /// </summary>
        public async void Stop(bool terminate)
        {
            if (!_started) return;

            // TODO I REALLY want to add some sort of check so that Stop would not proceed until all convertion operations finish.

            _aPath = null!;
            _bPath = null!;

            _watcher?.Dispose();
            _watcher = null!;

            _timer?.Dispose();
            _timer = null!;

            _started = false;
        }

        public void Reset()
        {
            this.Stop(false);
            this.Start();
        }

        /// <summary>
        /// Leads paths from config file
        /// </summary>
        /// <exception cref="FileNotFoundException"></exception>
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

        /// <summary>
        /// Setups FileSystemWatcher
        /// </summary>
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
                //Interval = DateTime.Today.AddDays(1).Subtract(DateTime.Now).TotalMilliseconds,
                Interval = 5000,
                AutoReset = true,
                Enabled= true,
            };

            // local function that swaps timer delay and event handler
            void swapInvoke(object? o, ElapsedEventArgs args)
            {
                // change delay
                //_timer.Interval = TimeSpan.FromDays(1).TotalMilliseconds;
                _timer.Interval = TimeSpan.FromMinutes(1).TotalMilliseconds;

                // swap handler
                _timer.Elapsed += OnMidnight;
                _timer.Elapsed -= swapInvoke;
                // invoke midnight
                OnMidnight(o, args);
            }
            _timer.Elapsed += swapInvoke;
            // this <s>trick</s> atrocious code allowes me to care about single timer.
        }

        private async void OnFileCreation(object? sender, FileSystemEventArgs args)
        {
            // TODO process file!!
            Console.WriteLine($"Processing {args.Name} file...");
            await Task.Delay(5000);
            Console.WriteLine($"Finished processing {args.Name} file");
        }

        private async void OnMidnight(object? sender, ElapsedEventArgs args)
        {
            // TODO save metadata.log
            Console.WriteLine("metadata.log was saved.");
            await Task.Delay(1000);
        }
    }
}
