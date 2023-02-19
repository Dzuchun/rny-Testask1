using Microsoft.VisualBasic.FileIO;
using System.Configuration;

namespace Test1Data
{
    public class Program
    {
        private static FileConverterService service;

        // I have NO C# filesystem interaction and proper CLI building experience at the start of this project.
        public static void Main(string[] args)
        {
            service = new ();
            ReactArgs(args);
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
                    service.Start();
                    Console.WriteLine("Service started");
                    break;
                case "reset":
                    service.Reset();
                    Console.WriteLine("Service was reset");
                    break;
                case "stop":
                    service.Stop(true);
                    service = null!;
                    Console.WriteLine("Stopping");
                    break;
                default:
                    Console.WriteLine("Unknown command");
                    break;
            }
        }

    }
}