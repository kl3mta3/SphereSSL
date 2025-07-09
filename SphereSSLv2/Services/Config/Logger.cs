using Microsoft.AspNetCore.SignalR;
using SphereSSLv2.Models;
using SphereSSLv2.Data;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using SphereSSLv2.Models.ConfigModels;
using SphereSSLv2.Data.Repositories;

namespace SphereSSLv2.Services.Config
{
    public class Logger
    {
        [DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int dwProcessId);
        internal static List<string> InMemoryLog = new();
        private const int ATTACH_PARENT_PROCESS = -1;

        private readonly IHubContext<SignalHub> _hubContext;

        public Logger(IHubContext<SignalHub> hubContext)
        {
            _hubContext = hubContext;
        }

        private LogRepository _logRepository
            ;
        internal static readonly string LogFilePath = Path.Combine(AppContext.BaseDirectory, "log.txt");

        public async Task Info(string message)
        {
            await Log("INFO", message);

        }

        public async Task Error(string message)
        {
            await Log("ERROR", message);
        }

        public async Task Debug(string message)
        {

            await Log("DEBUG", message);
        }

        public async Task Update(string message)
        {

            await Log("UPDATE", message);
        }

        private async Task Log(string level, string message)
        {
            var entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";

            var username = ExtractUsername(message);

            LogEntry logEntry = new LogEntry
            {
                Timestamp = DateTime.Now,
                AlertLevel = level,
                Message = message,
                Username = username
            };

            if (ConfigureService.IsSetup)
            {
                await LogRepository.InsertLog(logEntry);
            }

                lock (InMemoryLog)
            {
                InMemoryLog.Add(entry);
                if (InMemoryLog.Count > 100)
                    InMemoryLog.RemoveAt(0);
            }

            try
            {

                File.AppendAllText(LogFilePath, entry + Environment.NewLine);

                if (level == "UPDATE")
                {
                    await _hubContext.Clients.All.SendAsync("UpdateLog", $"{entry}");

                  

                }

                if (level == "ERROR")
                {
                    await _hubContext.Clients.All.SendAsync("ErrorLog", $"{entry}");
                    if (HasConsole())
                        Console.WriteLine(entry);

                }
                if (level == "INFO")
                {
                    await _hubContext.Clients.All.SendAsync("InfoLog", $"{entry}");

                }
                if (level == "Debug" && HasConsole())
                {
                    await _hubContext.Clients.All.SendAsync("DebugLog", $"{entry}");
                    if (HasConsole())
                        Console.WriteLine(entry);
                }

            }
            catch (Exception ex)
            {
                try
                {

                    await _hubContext.Clients.All.SendAsync("ErrorLog", $"[LOGGER ERROR] Failed to write to log file: {ex.Message}");
                    if (HasConsole())
                        Console.WriteLine($"Failed to log message: {ex.Message}");
                }
                catch
                {
                    // If we can't log the error, there's not much we can do.
                }
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

        private string ExtractUsername(string message)
        {
            // Message format: [username]:rest of message
            // Regex to grab anything between the first [ and ] before the colon
            var match = Regex.Match(message, @"^\[(.*?)\]:");
            return match.Success ? match.Groups[1].Value : "Unknown";
        }

    }


}

