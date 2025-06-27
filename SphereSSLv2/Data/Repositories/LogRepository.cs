using Microsoft.Data.Sqlite;
using SphereSSLv2.Models.ConfigModels;
using SphereSSLv2.Services.Config;

namespace SphereSSLv2.Data.Repositories
{
    public class LogRepository
    {

        //logs Management

        public static async Task InsertLog(LogEntry entry)
        {
            try
            {
                using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
                await connection.OpenAsync();
                var command = connection.CreateCommand();
                command.CommandText = @"
                    INSERT INTO Logs (UserId AlertLevel, LogId, Message, Timestamp)
                    VALUES ($userId, $alertLevel, $logId, $message, $timestamp)";
                command.Parameters.AddWithValue("$userId", entry.Username ?? string.Empty);
                command.Parameters.AddWithValue("$logId", entry.LogId);
                command.Parameters.AddWithValue("$alertLevel", entry.AlertLevel);
                command.Parameters.AddWithValue("$message", entry.Message);
                command.Parameters.AddWithValue("$timestamp", DateTime.UtcNow);
                await command.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error inserting log: {ex.Message}", ex);
            }
        }

        public static async Task<List<LogEntry>> GetLogs(string alertLevel = null)
        {
            List<LogEntry> logs = new List<LogEntry>();
            try
            {
                using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
                await connection.OpenAsync();
                var command = connection.CreateCommand();
                if (string.IsNullOrEmpty(alertLevel))
                {
                    command.CommandText = "SELECT * FROM Logs ORDER BY Timestamp DESC";
                }
                else
                {
                    command.CommandText = "SELECT * FROM Logs WHERE AlertLevel = @alertLevel ORDER BY Timestamp DESC";
                    command.Parameters.AddWithValue("@alertLevel", alertLevel);
                }
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    LogEntry log = new LogEntry
                    {
                        Username = reader.GetString(reader.GetOrdinal("UserId")),
                        LogId = reader.GetString(reader.GetOrdinal("LogId")),
                        AlertLevel = reader.GetString(reader.GetOrdinal("AlertLevel")),
                        Message = reader.GetString(reader.GetOrdinal("Message")),
                        Timestamp = reader.GetDateTime(reader.GetOrdinal("Timestamp"))
                    };
                    logs.Add(log);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error retrieving logs: {ex.Message}", ex);
            }
            return logs;
        }

        public static async Task<LogEntry> GetLogByID(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return null;

            try
            {
                using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
                await connection.OpenAsync();

                var command = connection.CreateCommand();
                command.CommandText = "SELECT * FROM Logs WHERE LogId = @logId";
                command.Parameters.AddWithValue("@logId", id);

                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return new LogEntry
                    {
                        Username = reader.GetString(reader.GetOrdinal("UserId")),
                        LogId = reader.GetString(reader.GetOrdinal("logId")),
                        AlertLevel = reader.GetString(reader.GetOrdinal("AlertLevel")),
                        Message = reader.GetString(reader.GetOrdinal("Message")),
                        Timestamp = reader.GetDateTime(reader.GetOrdinal("Timestamp"))
                    };
                }

                return null; // No match found
            }
            catch (Exception ex)
            {
                throw new Exception($"Error retrieving log by ID: {ex.Message}", ex);
            }
        }

        public static async Task<List<LogEntry>> GetLogsWithRange(string alertLevel = null, int range = 30)
        {
            List<LogEntry> logs = new List<LogEntry>();
            try
            {
                using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
                await connection.OpenAsync();
                var command = connection.CreateCommand();

                // Calculate the cutoff timestamp
                var cutoff = DateTime.UtcNow.AddDays(-range);

                if (string.IsNullOrEmpty(alertLevel))
                {
                    command.CommandText = @"
                SELECT * FROM Logs 
                WHERE Timestamp >= @cutoff 
                ORDER BY Timestamp DESC";
                }
                else
                {
                    command.CommandText = @"
                SELECT * FROM Logs 
                WHERE AlertLevel = @alertLevel AND Timestamp >= @cutoff 
                ORDER BY Timestamp DESC";
                    command.Parameters.AddWithValue("@alertLevel", alertLevel);
                }

                command.Parameters.AddWithValue("@cutoff", cutoff);

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    LogEntry log = new LogEntry
                    {
                        Username = reader.GetString(reader.GetOrdinal("UserId")),
                        LogId = reader.GetString(reader.GetOrdinal("LogId")),
                        AlertLevel = reader.GetString(reader.GetOrdinal("AlertLevel")),
                        Message = reader.GetString(reader.GetOrdinal("Message")),
                        Timestamp = reader.GetDateTime(reader.GetOrdinal("Timestamp"))
                    };
                    logs.Add(log);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error retrieving logs: {ex.Message}", ex);
            }

            return logs;
        }

        public static async Task<bool> DeleteLogByID(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return false;

            try
            {
                using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
                await connection.OpenAsync();

                var command = connection.CreateCommand();
                command.CommandText = "DELETE FROM Logs WHERE LogId = @logId";
                command.Parameters.AddWithValue("@logId", id);

                int rowsAffected = await command.ExecuteNonQueryAsync();
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error deleting log by ID: {ex.Message}", ex);
            }
        }
    }
}
