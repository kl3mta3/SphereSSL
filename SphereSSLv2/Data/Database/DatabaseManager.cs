using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualBasic.ApplicationServices;
using SphereSSLv2.Models.CertModels;
using SphereSSLv2.Models.ConfigModels;
using SphereSSLv2.Models.DNSModels;
using SphereSSLv2.Services.Config;
using System.Security.AccessControl;
using System.Security.Cryptography.Xml;

namespace SphereSSLv2.Data.Database
{
    public class DatabaseManager
    {
      
        internal readonly Logger _logger;
        public DatabaseManager(Logger logger)
        {
            _logger = logger;
        }

        //start DB
        public static async Task Initialize()
        {
            try
            {
                using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
                await connection.OpenAsync(); 
                using var pragma = connection.CreateCommand();
                pragma.CommandText = "PRAGMA foreign_keys = ON;";
                await pragma.ExecuteNonQueryAsync();

                var adminUUID= Guid.NewGuid();
                var adminUserId = Guid.NewGuid().ToString("N"); 
                var adminPassHash = ConfigureService.HashedPassword;

                var command = connection.CreateCommand();
                command.CommandText = @$"

                CREATE TABLE IF NOT EXISTS CertRecords (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId TEXT,
                    OrderId TEXT NOT NULL,
                    Domain TEXT NOT NULL,
                    Email TEXT NOT NULL,
                    DnsChallengeToken TEXT,
                    SavePath TEXT,
                    Provider TEXT,
                    CreationTime TEXT NOT NULL,
                    ExpiryDate TEXT NOT NULL,
                    UseSeparateFiles INTEGER DEFAULT 0,
                    SaveForRenewal INTEGER DEFAULT 0,
                    AutoRenew INTEGER DEFAULT 0,
                    FailedRenewals INTEGER DEFAULT 0,
                    SuccessfulRenewals INTEGER DEFAULT 0,
                    Signer TEXT,
                    AccountID TEXT,
                    OrderUrl TEXT,
                    ChallengeType TEXT,
                    Thumbprint TEXT,
                    ZoneId TEXT,
                    FOREIGN KEY(UserId) REFERENCES Users(Id) ON DELETE RESTRICT
                );

                CREATE TABLE IF NOT EXISTS ExpiredCerts (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId TEXT,
                    OrderId TEXT NOT NULL,
                    Domain TEXT NOT NULL,
                    Email TEXT NOT NULL,
                    DnsChallengeToken TEXT,
                    SavePath TEXT,
                    Provider TEXT,
                    CreationTime TEXT NOT NULL,
                    ExpiryDate TEXT NOT NULL,
                    UseSeparateFiles INTEGER DEFAULT 0,
                    SaveForRenewal INTEGER DEFAULT 0,
                    AutoRenew INTEGER DEFAULT 0,
                    FailedRenewals INTEGER DEFAULT 0,
                    SuccessfulRenewals INTEGER DEFAULT 0,
                    Signer TEXT,
                    AccountID TEXT,
                    OrderUrl TEXT,
                    ChallengeType TEXT,
                    Thumbprint TEXT,
                    ZoneId TEXT,
                    FOREIGN KEY(UserId) REFERENCES Users(Id) ON DELETE RESTRICT  
                );

                CREATE TABLE IF NOT EXISTS Health (
                    Id INTEGER PRIMARY KEY CHECK (Id = 1),
                    TotalCertsInDB INTEGER,
                    ExpiredCertCount INTEGER,
                    TotalDNSProviderCount INTEGER,
                    DateLastBooted TEXT
                );

                CREATE TABLE IF NOT EXISTS DbVersion (
                    Id INTEGER PRIMARY KEY CHECK (Id = 1),
                    Version INTEGER NOT NULL
                );

                CREATE TABLE IF NOT EXISTS DNSProviders (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId TEXT,
                    ProviderName TEXT,
                    Provider TEXT,
                    APIKey TEXT,
                    Ttl INTEGER,
                    FOREIGN KEY(UserId) REFERENCES Users(Id) ON DELETE CASCADE

                );

                CREATE TABLE IF NOT EXISTS Users(
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId TEXT NOT NULL UNIQUE,
                    Username TEXT,
                    PasswordHash TEXT,
                    Name TEXT,
                    Email TEXT,
                    CreationTime TEXT NOT NULL, 
                    LastUpdated TEXT,
                    UUID TEXT UNIQUE,
                    Notes TEXT
                ); 

                CREATE TABLE IF NOT EXISTS UserLogins (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId TEXT,
                    LoginTime TEXT,
                    IPAddress TEXT,
                    UserAgent TEXT,
                    Success BOOL,
                   FOREIGN KEY(UserId) REFERENCES Users(Id) ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS ApiKeys (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId TEXT,
                    ApiKey TEXT UNIQUE,
                    Created TEXT,
                    LastUsed TEXT,
                    IsRevoked BOOL DEFAULT 0,
                    FOREIGN KEY(UserId) REFERENCES Users(Id) ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS UserStats (
                    UserId TEXT PRIMARY KEY,
                    TotalCerts INTEGER DEFAULT 0,
                    CertsRenewed INTEGER DEFAULT 0,
                    CertsFailed INTEGER DEFAULT 0,
                    LastCertCreated TEXT,
                    FOREIGN KEY(UserId) REFERENCES Users(Id) ON DELETE CASCADE
                );

                 CREATE TABLE IF NOT EXISTS UserRoles (
                     UserId TEXT PRIMARY KEY,
                     IsAdmin BOOL,
                     IsEnabled BOOL,
                     Role TEXT DEFAULT 'User' CHECK(Role IN ('Viewer', 'User', 'Admin', 'SuperAdmin')),
                     FOREIGN KEY(UserId) REFERENCES Users(Id) ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS Logs(
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId TEXT,
                    LogId TEXT,
                    AlertLevel TEXT,
                    Message TEXT,
                    Timestamp DATETIME,
                    FOREIGN KEY(UserId) REFERENCES Users(Id) ON DELETE RESTRICT  
                ); 

                ";

                await command.ExecuteNonQueryAsync();

                // Insert default DB Version record
                command.CommandText = "INSERT OR IGNORE INTO DbVersion(Id, Version) VALUES(1, 1);";
                await command.ExecuteNonQueryAsync();


                // Insert defaultSuperAdmin User record
                command.CommandText = @"
                INSERT OR IGNORE INTO Users (
                UserId, Username, PasswordHash, Name, Email, CreationTime, LastUpdated, UUID, Notes
                )
                    VALUES (
                    @userId, 'Masterlocke', @passwordHash, 'Locke-Ann Key', 'admin@example.com',
                    datetime('now'), datetime('now'), @uuid, 'System default super admin account'
                );";

                command.Parameters.Clear();
                command.Parameters.AddWithValue("@userId", adminUserId);
                command.Parameters.AddWithValue("@passwordHash", adminPassHash);
                command.Parameters.AddWithValue("@uuid", adminUUID.ToString());
                await command.ExecuteNonQueryAsync();

                // Insert default UserRole record
                command.CommandText = @"
                INSERT OR IGNORE INTO UserRoles (UserId, IsAdmin, IsEnabled, Role)
                    VALUES (@userId, 1, 1, 'SuperAdmin');
                ";
                command.Parameters.Clear();
                command.Parameters.AddWithValue("@userId", adminUserId);
                await command.ExecuteNonQueryAsync();


                // Insert default log record
                command.CommandText = @"
                INSERT INTO Logs (UserId, LogID, AlertLevel, Message, Timestamp)
                    VALUES (@userId, @logId, 'INFO', 'Locke-Ann Key has entered the realm.', datetime('now'));
                ";
                command.Parameters.Clear();
                command.Parameters.AddWithValue("@userId", adminUserId);
                command.Parameters.AddWithValue("@logId", Guid.NewGuid().ToString("N"));
                await command.ExecuteNonQueryAsync();

                await HealthRepository.RecalculateHealthStats();
            }
            catch (Exception ex)
            {

            }
        }





        //DB Version and Migration
        public static async Task<int> GetDatabaseVersion()
        {
            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT Version FROM DbVersion WHERE Id = 1";

            var result = await command.ExecuteScalarAsync();
            return result == null ? 0 : Convert.ToInt32(result);
        }

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
                command.Parameters.AddWithValue("$userId", entry.UserId ?? string.Empty);
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
                        UserId = reader.GetString(reader.GetOrdinal("UserId")),
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
                        UserId = reader.GetString(reader.GetOrdinal("UserId")),
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
                        UserId = reader.GetString(reader.GetOrdinal("UserId")),
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
