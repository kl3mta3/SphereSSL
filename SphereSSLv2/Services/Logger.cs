using SphereSSLv2.Data;
using System.Runtime.InteropServices;

namespace SphereSSLv2.Services
{
    public static class Logger
    {
        [DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int dwProcessId);

        private const int ATTACH_PARENT_PROCESS = -1;

        internal static readonly string LogFilePath = Path.Combine(AppContext.BaseDirectory, "log.txt");

        public static void Info(string message)
        {
            Log("INFO", message);
        }

        public static void Error(string message)
        {
            Log("ERROR", message);
        }

        public static void Debug(string message)
        {
            Log("DEBUG", message);
        }

        private static void Log(string level, string message)
        {
            var entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";
            File.AppendAllText(LogFilePath, entry + Environment.NewLine);
            if (level == "ERROR" && HasConsole())
            {
                Console.WriteLine(entry);

            }
        }

        private static bool HasConsole()
        {
            try
            {
                return Console.OpenStandardOutput(1) != Stream.Null;
            }
            catch
            {
                return false;
            }
        }
    }


}

