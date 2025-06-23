using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualBasic.ApplicationServices;
using SphereSSLv2.Models.CertModels;
using SphereSSLv2.Models.ConfigModels;
using SphereSSLv2.Models.DNSModels;
using SphereSSLv2.Services.Config;
using System.Security.AccessControl;
using System.Security.Cryptography.Xml;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;

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
                var adminUsername = ConfigureService.Username;
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
                    FOREIGN KEY(UserId) REFERENCES Users(UserId) ON DELETE RESTRICT
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
                    FOREIGN KEY(UserId) REFERENCES Users(UserId) ON DELETE RESTRICT  
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
                    FOREIGN KEY(UserId) REFERENCES Users(UserId) ON DELETE CASCADE

                );

                CREATE TABLE IF NOT EXISTS Users(
                    UserId TEXT PRIMARY KEY,
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
                   FOREIGN KEY(UserId) REFERENCES Users(UserId) ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS ApiKeys (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId TEXT,
                    ApiKey TEXT UNIQUE,
                    Created TEXT,
                    LastUsed TEXT,
                    IsRevoked BOOL DEFAULT 0,
                    FOREIGN KEY(UserId) REFERENCES Users(UserId) ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS UserStats (
                    UserId TEXT PRIMARY KEY,
                    TotalCerts INTEGER DEFAULT 0,
                    CertsRenewed INTEGER DEFAULT 0,
                    CertsFailed INTEGER DEFAULT 0,
                    LastCertCreated TEXT,
                    FOREIGN KEY(UserId) REFERENCES Users(UserId) ON DELETE CASCADE
                );

                 CREATE TABLE IF NOT EXISTS UserRoles (
                     UserId TEXT PRIMARY KEY,
                     IsAdmin BOOL,
                     IsEnabled BOOL,
                     Role TEXT DEFAULT 'User' CHECK(Role IN ('Viewer', 'User', 'Admin', 'SuperAdmin')),
                     FOREIGN KEY(UserId) REFERENCES Users(UserId) ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS Logs(
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId TEXT,
                    LogId TEXT,
                    AlertLevel TEXT,
                    Message TEXT,
                    Timestamp DATETIME,
                    FOREIGN KEY(UserId) REFERENCES Users(UserId) ON DELETE CASCADE
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
                    @userId, @username, @passwordHash, 'Locke-Ann Key', 'admin@example.com',
                    datetime('now'), datetime('now'), @uuid, 'System default super admin account'
                );"
                ;

                command.Parameters.Clear();
                command.Parameters.AddWithValue("@username", adminUsername);
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

    }
}
