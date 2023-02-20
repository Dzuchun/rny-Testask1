using Microsoft.VisualBasic.FileIO;
using System.Configuration;

namespace Test1Data
{
    public static class Program
    {
        private static FileConverterService service = null!;

        // I have NO C# filesystem interaction and proper CLI building experience at the start of this project.
        public static void Main(string[] args)
        {
            service = new();
            ReactArgs(args);
            Console.WriteLine("Service ready to start.");
            while (service != null)
            {
                args = GetNextArgs();
                ReactArgs(args);
                Console.WriteLine();
            }
        }

        private static string[] GetNextArgs()
        {
            return Console.ReadLine()!.Split(' ');
        }

        private static void ReactArgs(string[] args)
        {
            if (args.Length < 1 || string.IsNullOrEmpty(args[0])) return;

            switch (args[0])
            {
                case "start":
                    try
                    {
                        service.Start();
                        Console.WriteLine("Service started");
                    }
                    catch(FileNotFoundException e)
                    {
                        Console.WriteLine($"Exception was cought during service start: {e.Message}");
                        Console.WriteLine("Please, check that \"App.config\" file exists and configured properly. \"App.config.template\" file may help you with that.");
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Exception was cought during service start: {e.Message}");
                    }
                    break;
                case "reset":
                case "restart":
                    try
                    {
                        service.Restart();
                        Console.WriteLine("Service was restarted");
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Exception was cought during service restart: {e.Message}");
                    }
                    break;
                case "stop":
                    try
                    {
                        service.Stop();
                        service = null!;
                        Console.WriteLine("Service was stopped");
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Exception was cought during service stop: {e.Message}");
                    }
                    break;
                default:
                    Console.WriteLine("Unknown command");
                    break;
            }
        }

    }
}