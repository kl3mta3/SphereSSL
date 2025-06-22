using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualBasic.ApplicationServices;
using SphereSSLv2.Models;
using SphereSSLv2.Services;
using System.Security.AccessControl;
using System.Security.Cryptography.Xml;

namespace SphereSSLv2.Data
{
    public class DatabaseManager
    {
        private readonly Logger _logger;
        public DatabaseManager(Logger logger)
        {
            _logger = logger;
        }

        //start DB
        public static async Task Initialize()
        {
            try
            {
                using var connection = new SqliteConnection($"Data Source={Spheressl.dbPath}");
                await connection.OpenAsync(); 
                using var pragma = connection.CreateCommand();
                pragma.CommandText = "PRAGMA foreign_keys = ON;";
                await pragma.ExecuteNonQueryAsync();

                var adminUUID= Guid.NewGuid();
                var adminUserId = Guid.NewGuid().ToString("N"); 
                var adminPassHash = Spheressl.HashedPassword;

                var command = connection.CreateCommand();
                command.CommandText = @$"

                CREATE TABLE IF NOT EXISTS CertRecords (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER,
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
                    UserId INTEGER,
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
                    UserId INTEGER,
                    ProviderName TEXT,
                    Provider TEXT,
                    APIKey TEXT,
                    Ttl INTEGER,
                    FOREIGN KEY(UserId) REFERENCES Users(Id) ON DELETE CASCADE

                );

                CREATE TABLE IF NOT EXISTS Users(
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER NOT NULL UNIQUE,
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
                    UserId INTEGER,
                    LoginTime TEXT,
                    IPAddress TEXT,
                    UserAgent TEXT,
                    Success BOOL,
                   FOREIGN KEY(UserId) REFERENCES Users(Id) ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS ApiKeys (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER,
                    ApiKey TEXT UNIQUE,
                    Created TEXT,
                    LastUsed TEXT,
                    IsRevoked BOOL DEFAULT 0,
                    FOREIGN KEY(UserId) REFERENCES Users(Id) ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS UserStats (
                    UserId INTEGER PRIMARY KEY,
                    TotalCerts INTEGER DEFAULT 0,
                    CertsRenewed INTEGER DEFAULT 0,
                    CertsFailed INTEGER DEFAULT 0,
                    LastCertCreated TEXT,
                    FOREIGN KEY(UserId) REFERENCES Users(Id) ON DELETE CASCADE
                );

                 CREATE TABLE IF NOT EXISTS UserRoles (
                     UserId INTEGER PRIMARY KEY,
                     IsAdmin BOOL,
                     IsEnabled BOOL,
                     Role TEXT DEFAULT 'User' CHECK(Role IN ('Viewer', 'User', 'Admin', 'SuperAdmin')),
                     FOREIGN KEY(UserId) REFERENCES Users(Id) ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS Logs(
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER,
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

                await RecalculateHealthStats();
            }
            catch (Exception ex)
            {

            }
        }


        //CertRecord Management
        public static async Task InsertCertRecord(CertRecord record)
        {
      

            using var connection = new SqliteConnection($"Data Source={Spheressl.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();

            command.CommandText = @"
        INSERT INTO CertRecords (
            OrderId, Domain, Email, DnsChallengeToken, SavePath, Provider,
            CreationTime, ExpiryDate, UseSeparateFiles, SaveForRenewal, AutoRenew,
            FailedRenewals, SuccessfulRenewals, Signer, AccountID, OrderUrl,
            ChallengeType, Thumbprint, ZoneId
        )
        VALUES (
            @OrderId, @Domain, @Email, @DnsChallengeToken, @SavePath, @Provider,
            @CreationTime, @ExpiryDate, @UseSeparateFiles, @SaveForRenewal, @AutoRenew,
            @FailedRenewals, @SuccessfulRenewals, @Signer, @AccountID, @OrderUrl,
            @ChallengeType, @Thumbprint, @ZoneId
        );";

            command.Parameters.AddWithValue("@OrderId", record.OrderId);
            command.Parameters.AddWithValue("@Domain", record.Domain);
            command.Parameters.AddWithValue("@Email", record.Email);
            command.Parameters.AddWithValue("@DnsChallengeToken", record.DnsChallengeToken);
            command.Parameters.AddWithValue("@SavePath", record.SavePath);
            command.Parameters.AddWithValue("@Provider", record.Provider);
            command.Parameters.AddWithValue("@CreationTime", record.CreationDate.ToString("o"));
            command.Parameters.AddWithValue("@ExpiryDate", record.ExpiryDate.ToString("o"));
            command.Parameters.AddWithValue("@UseSeparateFiles", record.UseSeparateFiles ? 1 : 0);
            command.Parameters.AddWithValue("@SaveForRenewal", record.SaveForRenewal ? 1 : 0);
            command.Parameters.AddWithValue("@AutoRenew", record.autoRenew ? 1 : 0);
            command.Parameters.AddWithValue("@FailedRenewals", record.FailedRenewals);
            command.Parameters.AddWithValue("@SuccessfulRenewals", record.SuccessfulRenewals);
            command.Parameters.AddWithValue("@Signer", record.Signer);
            command.Parameters.AddWithValue("@AccountID", record.AccountID);
            command.Parameters.AddWithValue("@OrderUrl", record.OrderUrl);
            command.Parameters.AddWithValue("@ChallengeType", record.ChallengeType);
            command.Parameters.AddWithValue("@Thumbprint", record.Thumbprint);
            command.Parameters.AddWithValue("@ZoneId", record.ZoneId ?? string.Empty);

            await command.ExecuteNonQueryAsync();
            await AdjustTotalCertsInDB(1);

            if (!Spheressl.CertRecords.Any(r => r.OrderId == record.OrderId))
            {
                Spheressl.CertRecords.Add(record);

             
            }
        }

        public static async Task UpdateCertRecord(CertRecord record)
        {
            using var connection = new SqliteConnection($"Data Source={Spheressl.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();

            command.CommandText = @"
        UPDATE CertRecords SET
            Domain = @Domain,
            Email = @Email,
            DnsChallengeToken = @DnsChallengeToken,
            SavePath = @SavePath,
            Provider = @Provider,
            CreationTime = @CreationTime,
            ExpiryDate = @ExpiryDate,
            UseSeparateFiles = @UseSeparateFiles,
            SaveForRenewal = @SaveForRenewal,
            AutoRenew = @AutoRenew,
            FailedRenewals = @FailedRenewals,
            SuccessfulRenewals = @SuccessfulRenewals,
            Signer = @Signer,
            AccountID = @AccountID,
            OrderUrl = @OrderUrl,
            ChallengeType = @ChallengeType,
            Thumbprint = @Thumbprint,
            ZoneId = @ZoneId

        WHERE OrderId = @OrderId";

            command.Parameters.AddWithValue("@OrderId", record.OrderId);
            command.Parameters.AddWithValue("@Domain", record.Domain);
            command.Parameters.AddWithValue("@Email", record.Email);
            command.Parameters.AddWithValue("@DnsChallengeToken", record.DnsChallengeToken);
            command.Parameters.AddWithValue("@SavePath", record.SavePath);
            command.Parameters.AddWithValue("@Provider", record.Provider);
            command.Parameters.AddWithValue("@CreationTime", record.CreationDate.ToString("o"));
            command.Parameters.AddWithValue("@ExpiryDate", record.ExpiryDate.ToString("o"));
            command.Parameters.AddWithValue("@UseSeparateFiles", record.UseSeparateFiles ? 1 : 0);
            command.Parameters.AddWithValue("@SaveForRenewal", record.SaveForRenewal ? 1 : 0);
            command.Parameters.AddWithValue("@AutoRenew", record.autoRenew ? 1 : 0);
            command.Parameters.AddWithValue("@FailedRenewals", record.FailedRenewals);
            command.Parameters.AddWithValue("@SuccessfulRenewals", record.SuccessfulRenewals);
            command.Parameters.AddWithValue("@Signer", record.Signer);
            command.Parameters.AddWithValue("@AccountID", record.AccountID);
            command.Parameters.AddWithValue("@OrderUrl", record.OrderUrl);
            command.Parameters.AddWithValue("@ChallengeType", record.ChallengeType);
            command.Parameters.AddWithValue("@Thumbprint", record.Thumbprint);
            command.Parameters.AddWithValue("@ZoneId", record.ZoneId ?? string.Empty);

            await command.ExecuteNonQueryAsync();
        }

        public static async Task DeleteCertRecordByOrderId(string orderId, SqliteConnection? connection = null, SqliteTransaction? transaction = null)
        {
            if (connection == null)
            {
                connection = new SqliteConnection($"Data Source={Spheressl.dbPath}");
            }
            await connection.OpenAsync();

            var command = connection.CreateCommand();

            if (transaction != null)
            {

                command.Transaction = transaction;
            }

            command.CommandText = @"
        DELETE FROM CertRecords
        WHERE OrderId = @OrderId;
    ";

            command.Parameters.AddWithValue("@OrderId", orderId);

            await command.ExecuteNonQueryAsync();

            await AdjustTotalCertsInDB(-1);

            var recordToRemove = Spheressl.CertRecords.FirstOrDefault(r => r.OrderId == orderId);
            if (recordToRemove != null)
            {
                Spheressl.CertRecords.Remove(recordToRemove);
            }
        }

        public static async Task<CertRecord?> GetCertRecordByOrderId(string orderId)
        {
            using var connection = new SqliteConnection($"Data Source={Spheressl.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
        SELECT * FROM CertRecords
        WHERE OrderId = @OrderId;
    ";

            command.Parameters.AddWithValue("@OrderId", orderId);

            using var reader = await command.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                return new CertRecord
                {
                    OrderId = reader["OrderId"].ToString(),
                    Domain = reader["Domain"].ToString(),
                    Email = reader["Email"].ToString(),
                    DnsChallengeToken = reader["DnsChallengeToken"].ToString(),
                    SavePath = reader["SavePath"].ToString(),
                    Provider = reader["Provider"].ToString(),
                    CreationDate = DateTime.Parse(reader["CreationTime"].ToString() ?? DateTime.MinValue.ToString()),
                    ExpiryDate = DateTime.Parse(reader["ExpiryDate"].ToString() ?? DateTime.MinValue.ToString()),
                    UseSeparateFiles = Convert.ToBoolean(reader["UseSeparateFiles"]),
                    SaveForRenewal = Convert.ToBoolean(reader["SaveForRenewal"]),
                    autoRenew = Convert.ToBoolean(reader["AutoRenew"]),
                    FailedRenewals = Convert.ToInt32(reader["FailedRenewals"]),
                    SuccessfulRenewals = Convert.ToInt32(reader["SuccessfulRenewals"]),
                    Signer = reader["Signer"].ToString(),
                    AccountID = reader["AccountID"].ToString(),
                    OrderUrl = reader["OrderUrl"].ToString(),
                    ChallengeType = reader["ChallengeType"].ToString(),
                    Thumbprint = reader["Thumbprint"].ToString(),
                    ZoneId = reader["ZoneId"].ToString()
                };
            }

            return null;
        }

        public static async Task<CertRecord?> GetCertRecordByDomain(string domain)
        {
            using var connection = new SqliteConnection($"Data Source={Spheressl.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
        SELECT * FROM CertRecords
        WHERE Domain = @Domain
        LIMIT 1;
    ";

            command.Parameters.AddWithValue("@Domain", domain);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new CertRecord
                {
                    OrderId = reader["OrderId"].ToString(),
                    Domain = reader["Domain"].ToString(),
                    Email = reader["Email"].ToString(),
                    DnsChallengeToken = reader["DnsChallengeToken"].ToString(),
                    SavePath = reader["SavePath"].ToString(),
                    Provider = reader["Provider"].ToString(),
                    CreationDate = DateTime.Parse(reader["CreationTime"].ToString() ?? DateTime.MinValue.ToString()),
                    ExpiryDate = DateTime.Parse(reader["ExpiryDate"].ToString() ?? DateTime.MinValue.ToString()),
                    UseSeparateFiles = Convert.ToBoolean(reader["UseSeparateFiles"]),
                    SaveForRenewal = Convert.ToBoolean(reader["SaveForRenewal"]),
                    autoRenew = Convert.ToBoolean(reader["AutoRenew"]),
                    FailedRenewals = Convert.ToInt32(reader["FailedRenewals"]),
                    SuccessfulRenewals = Convert.ToInt32(reader["SuccessfulRenewals"]),
                    Signer = reader["Signer"].ToString(),
                    AccountID = reader["AccountID"].ToString(),
                    OrderUrl = reader["OrderUrl"].ToString(),
                    ChallengeType = reader["ChallengeType"].ToString(),
                    Thumbprint = reader["Thumbprint"].ToString(),
                    ZoneId = reader["ZoneId"].ToString()
                };
            }

            return null;
        }

        public static async Task<List<CertRecord>> GetAllCertRecords()
        {
            var records = new List<CertRecord>();

            using var connection = new SqliteConnection($"Data Source={Spheressl.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
        SELECT 
            OrderId, Domain, Email, DnsChallengeToken, SavePath, Provider,
            CreationTime, ExpiryDate, UseSeparateFiles, SaveForRenewal, AutoRenew,
            FailedRenewals, SuccessfulRenewals, Signer, AccountID, OrderUrl,
            ChallengeType, Thumbprint, ZoneId
        FROM CertRecords;
    ";

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var record = new CertRecord
                {
                    OrderId = reader.GetString(0),
                    Domain = reader.GetString(1),
                    Email = reader.GetString(2),
                    DnsChallengeToken = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    SavePath = reader.IsDBNull(4) ? "" : reader.GetString(4),
                    Provider = reader.IsDBNull(5) ? "" : reader.GetString(5),
                    CreationDate = DateTime.Parse(reader.GetString(6)),
                    ExpiryDate = DateTime.Parse(reader.GetString(7)),
                    UseSeparateFiles = reader.GetInt32(8) != 0,
                    SaveForRenewal = reader.GetInt32(9) != 0,
                    autoRenew = reader.GetInt32(10) != 0,
                    FailedRenewals = reader.GetInt32(11),
                    SuccessfulRenewals = reader.GetInt32(12),
                    Signer = reader.IsDBNull(13) ? "" : reader.GetString(13),
                    AccountID = reader.IsDBNull(14) ? "" : reader.GetString(14),
                    OrderUrl = reader.IsDBNull(15) ? "" : reader.GetString(15),
                    ChallengeType = reader.IsDBNull(16) ? "" : reader.GetString(16),
                    Thumbprint = reader.IsDBNull(17) ? "" : reader.GetString(17),
                    ZoneId = reader.IsDBNull(18) ? "" : reader.GetString(18),
                };

                records.Add(record);
            }

            return records;
        }

        public static async Task DeleteAllCertRecords()
        {
            using var connection = new SqliteConnection($"Data Source={Spheressl.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM CertRecords;";
            await command.ExecuteNonQueryAsync();
            await ClearTotalCertsInDB();
        }

        public static async Task<List<CertRecord>> GetExpiringSoonCerts(int daysThreshold = 30)
        {
            var records = new List<CertRecord>();

            using var connection = new SqliteConnection($"Data Source={Spheressl.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
        SELECT * FROM CertRecords
        WHERE julianday(ExpiryDate) - julianday('now') <= @Threshold;
    ";

            command.Parameters.AddWithValue("@Threshold", daysThreshold);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var cert = new CertRecord
                {
                    OrderId = reader["OrderId"].ToString(),
                    Domain = reader["Domain"].ToString(),
                    Email = reader["Email"].ToString(),
                    DnsChallengeToken = reader["DnsChallengeToken"].ToString(),
                    SavePath = reader["SavePath"].ToString(),
                    Provider = reader["Provider"].ToString(),
                    CreationDate = DateTime.Parse(reader["CreationTime"].ToString() ?? DateTime.MinValue.ToString()),
                    ExpiryDate = DateTime.Parse(reader["ExpiryDate"].ToString() ?? DateTime.MinValue.ToString()),
                    UseSeparateFiles = Convert.ToBoolean(reader["UseSeparateFiles"]),
                    SaveForRenewal = Convert.ToBoolean(reader["SaveForRenewal"]),
                    autoRenew = Convert.ToBoolean(reader["AutoRenew"]),
                    FailedRenewals = Convert.ToInt32(reader["FailedRenewals"]),
                    SuccessfulRenewals = Convert.ToInt32(reader["SuccessfulRenewals"]),
                    Signer = reader["Signer"].ToString(),
                    AccountID = reader["AccountID"].ToString(),
                    OrderUrl = reader["OrderUrl"].ToString(),
                    ChallengeType = reader["ChallengeType"].ToString(),
                    Thumbprint = reader["Thumbprint"].ToString(),
                    ZoneId = reader["ZoneId"].ToString()
                };
                records.Add(cert);
            }

            return records;
        }

        public static async Task<List<CertRecord>> GetCertRecordsByEmail(string email)
        {
            var records = new List<CertRecord>();

            using var connection = new SqliteConnection($"Data Source={Spheressl.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
        SELECT * FROM CertRecords
        WHERE Email = @Email;
    ";

            command.Parameters.AddWithValue("@Email", email);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var record = new CertRecord
                {
                    OrderId = reader["OrderId"].ToString(),
                    Domain = reader["Domain"].ToString(),
                    Email = reader["Email"].ToString(),
                    DnsChallengeToken = reader["DnsChallengeToken"].ToString(),
                    SavePath = reader["SavePath"].ToString(),
                    Provider = reader["Provider"].ToString(),
                    CreationDate = DateTime.Parse(reader["CreationTime"].ToString() ?? DateTime.MinValue.ToString()),
                    ExpiryDate = DateTime.Parse(reader["ExpiryDate"].ToString() ?? DateTime.MinValue.ToString()),
                    UseSeparateFiles = Convert.ToBoolean(reader["UseSeparateFiles"]),
                    SaveForRenewal = Convert.ToBoolean(reader["SaveForRenewal"]),
                    autoRenew = Convert.ToBoolean(reader["AutoRenew"]),
                    FailedRenewals = Convert.ToInt32(reader["FailedRenewals"]),
                    SuccessfulRenewals = Convert.ToInt32(reader["SuccessfulRenewals"]),
                    Signer = reader["Signer"].ToString(),
                    AccountID = reader["AccountID"].ToString(),
                    OrderUrl = reader["OrderUrl"].ToString(),
                    ChallengeType = reader["ChallengeType"].ToString(),
                    Thumbprint = reader["Thumbprint"].ToString(),
                    ZoneId = reader["ZoneId"].ToString()
                };

                records.Add(record);
            }

            return records;
        }

        public static async Task<List<CertRecord>> GetAllExpiredCerts()
        {
            var records = new List<CertRecord>();
            using var connection = new SqliteConnection($"Data Source={Spheressl.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
            SELECT * FROM CertRecords;
            ";

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var record = new CertRecord
                {
                    OrderId = reader.GetString(1),
                    Domain = reader.GetString(2),
                    Email = reader.GetString(3),
                    DnsChallengeToken = reader.IsDBNull(4) ? "" : reader.GetString(4),
                    SavePath = reader.IsDBNull(5) ? "" : reader.GetString(5),
                    Provider = reader.IsDBNull(6) ? "" : reader.GetString(6),
                    CreationDate = DateTime.Parse(reader.GetString(7)),
                    ExpiryDate = DateTime.Parse(reader.GetString(8)),
                    UseSeparateFiles = reader.GetInt32(9) != 0,
                    SaveForRenewal = reader.GetInt32(10) != 0,
                    autoRenew = reader.GetInt32(11) != 0,
                    FailedRenewals = reader.GetInt32(12),
                    SuccessfulRenewals = reader.GetInt32(13),
                    Signer = reader.IsDBNull(14) ? "" : reader.GetString(14),
                    AccountID = reader.IsDBNull(15) ? "" : reader.GetString(15),
                    OrderUrl = reader.IsDBNull(16) ? "" : reader.GetString(16),
                    ChallengeType = reader.IsDBNull(17) ? "" : reader.GetString(17),
                    Thumbprint = reader.IsDBNull(18) ? "" : reader.GetString(18),
                    ZoneId = reader.IsDBNull(19) ? "" : reader.GetString(19)
                };
                records.Add(record);
            }

            return records;
        }

        public static async Task MigrateExpiredCert()
        {
            List<CertRecord> certList = await GetAllExpiredCerts();
            if (certList == null || certList.Count == 0) return;

            using var connection = new SqliteConnection($"Data Source={Spheressl.dbPath}");
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();

            foreach (CertRecord record in certList)
            {
                await InsertExpiredCert(record, connection, transaction);
                await DeleteCertRecordByOrderId(record.OrderId, connection, transaction);
            }

            await transaction.CommitAsync();
        }



        //Expired cert Management
        public static async Task InsertExpiredCert(CertRecord record, SqliteConnection? connection = null, SqliteTransaction? transaction = null)
        {
            if (connection == null)
            {
                connection = new SqliteConnection($"Data Source={Spheressl.dbPath}");
            }

            await connection.OpenAsync();

            var command = connection.CreateCommand();

            if (transaction != null)
            {

                command.Transaction = transaction;
            }

            command.CommandText = @"
        INSERT INTO ExpiredCerts (
            OrderId, Domain, Email, DnsChallengeToken, SavePath, Provider,
            CreationTime, ExpiryDate, UseSeparateFiles, SaveForRenewal, AutoRenew,
            FailedRenewals, SuccessfulRenewals, Signer, AccountID, OrderUrl,
            ChallengeType, Thumbprint, ZoneId
        )
        VALUES (
            @OrderId, @Domain, @Email, @DnsChallengeToken, @SavePath, @Provider,
            @CreationTime, @ExpiryDate, @UseSeparateFiles, @SaveForRenewal, @AutoRenew,
            @FailedRenewals, @SuccessfulRenewals, @Signer, @AccountID, @OrderUrl,
            @ChallengeType, @Thumbprint, @ZoneId
        );";

            command.Parameters.AddWithValue("@OrderId", record.OrderId);
            command.Parameters.AddWithValue("@Domain", record.Domain);
            command.Parameters.AddWithValue("@Email", record.Email);
            command.Parameters.AddWithValue("@DnsChallengeToken", record.DnsChallengeToken);
            command.Parameters.AddWithValue("@SavePath", record.SavePath);
            command.Parameters.AddWithValue("@Provider", record.Provider);
            command.Parameters.AddWithValue("@CreationTime", record.CreationDate.ToString("o"));
            command.Parameters.AddWithValue("@ExpiryDate", record.ExpiryDate.ToString("o"));
            command.Parameters.AddWithValue("@UseSeparateFiles", record.UseSeparateFiles ? 1 : 0);
            command.Parameters.AddWithValue("@SaveForRenewal", record.SaveForRenewal ? 1 : 0);
            command.Parameters.AddWithValue("@AutoRenew", record.autoRenew ? 1 : 0);
            command.Parameters.AddWithValue("@FailedRenewals", record.FailedRenewals);
            command.Parameters.AddWithValue("@SuccessfulRenewals", record.SuccessfulRenewals);
            command.Parameters.AddWithValue("@Signer", record.Signer);
            command.Parameters.AddWithValue("@AccountID", record.AccountID);
            command.Parameters.AddWithValue("@OrderUrl", record.OrderUrl);
            command.Parameters.AddWithValue("@ChallengeType", record.ChallengeType);
            command.Parameters.AddWithValue("@Thumbprint", record.Thumbprint);
            command.Parameters.AddWithValue("@ZoneId", record.ZoneId ?? string.Empty);

            await command.ExecuteNonQueryAsync();
            await AdjustExpiredCertCountInDB(1);

            if (!Spheressl.ExpiredCertRecords.Any(r => r.OrderId == record.OrderId))
            {
                Spheressl.ExpiredCertRecords.Add(record);
            }

        }

        public static async Task DeleteExpiredCertsByOrderId(string orderId)
        {
            using var connection = new SqliteConnection($"Data Source={Spheressl.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
            DELETE FROM ExpiredCerts
            WHERE OrderId = @OrderId;
            ";

            command.Parameters.AddWithValue("@OrderId", orderId);

            await command.ExecuteNonQueryAsync();
            await AdjustExpiredCertCountInDB(-1);

            var recordToRemove = Spheressl.ExpiredCertRecords.FirstOrDefault(r => r.OrderId == orderId);
            if (recordToRemove != null)
            {
                Spheressl.ExpiredCertRecords.Remove(recordToRemove);
            }

        }

        public static async Task<CertRecord?> GetExpiredCertByOrderId(string orderId)
        {
            using var connection = new SqliteConnection($"Data Source={Spheressl.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
        SELECT * FROM ExpiredCerts
        WHERE OrderId = @OrderId;
    ";

            command.Parameters.AddWithValue("@OrderId", orderId);

            using var reader = await command.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                return new CertRecord
                {
                    OrderId = reader["OrderId"].ToString(),
                    Domain = reader["Domain"].ToString(),
                    Email = reader["Email"].ToString(),
                    DnsChallengeToken = reader["DnsChallengeToken"].ToString(),
                    SavePath = reader["SavePath"].ToString(),
                    Provider = reader["Provider"].ToString(),
                    CreationDate = DateTime.Parse(reader["CreationTime"].ToString() ?? DateTime.MinValue.ToString()),
                    ExpiryDate = DateTime.Parse(reader["ExpiryDate"].ToString() ?? DateTime.MinValue.ToString()),
                    UseSeparateFiles = Convert.ToBoolean(reader["UseSeparateFiles"]),
                    SaveForRenewal = Convert.ToBoolean(reader["SaveForRenewal"]),
                    autoRenew = Convert.ToBoolean(reader["AutoRenew"]),
                    FailedRenewals = Convert.ToInt32(reader["FailedRenewals"]),
                    SuccessfulRenewals = Convert.ToInt32(reader["SuccessfulRenewals"]),
                    Signer = reader["Signer"].ToString(),
                    AccountID = reader["AccountID"].ToString(),
                    OrderUrl = reader["OrderUrl"].ToString(),
                    ChallengeType = reader["ChallengeType"].ToString(),
                    Thumbprint = reader["Thumbprint"].ToString(),
                    ZoneId = reader["ZoneId"].ToString()
                };
            }

            return null;
        }

        public static async Task<CertRecord?> GetExpiredCertByDomain(string domain)
        {
            using var connection = new SqliteConnection($"Data Source={Spheressl.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
        SELECT * FROM ExpiredCerts
        WHERE Domain = @Domain
        LIMIT 1;
    ";

            command.Parameters.AddWithValue("@Domain", domain);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new CertRecord
                {
                    OrderId = reader["OrderId"].ToString(),
                    Domain = reader["Domain"].ToString(),
                    Email = reader["Email"].ToString(),
                    DnsChallengeToken = reader["DnsChallengeToken"].ToString(),
                    SavePath = reader["SavePath"].ToString(),
                    Provider = reader["Provider"].ToString(),
                    CreationDate = DateTime.Parse(reader["CreationTime"].ToString() ?? DateTime.MinValue.ToString()),
                    ExpiryDate = DateTime.Parse(reader["ExpiryDate"].ToString() ?? DateTime.MinValue.ToString()),
                    UseSeparateFiles = Convert.ToBoolean(reader["UseSeparateFiles"]),
                    SaveForRenewal = Convert.ToBoolean(reader["SaveForRenewal"]),
                    autoRenew = Convert.ToBoolean(reader["AutoRenew"]),
                    FailedRenewals = Convert.ToInt32(reader["FailedRenewals"]),
                    SuccessfulRenewals = Convert.ToInt32(reader["SuccessfulRenewals"]),
                    Signer = reader["Signer"].ToString(),
                    AccountID = reader["AccountID"].ToString(),
                    OrderUrl = reader["OrderUrl"].ToString(),
                    ChallengeType = reader["ChallengeType"].ToString(),
                    Thumbprint = reader["Thumbprint"].ToString(),
                    ZoneId = reader["ZoneId"].ToString()
                };
            }

            return null;
        }

        public static async Task<List<CertRecord>> GetExpiredCertsByEmail(string email)
        {
            var records = new List<CertRecord>();

            using var connection = new SqliteConnection($"Data Source={Spheressl.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
        SELECT * FROM ExpiredCerts
        WHERE Email = @Email;
    ";

            command.Parameters.AddWithValue("@Email", email);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var record = new CertRecord
                {
                    OrderId = reader["OrderId"].ToString(),
                    Domain = reader["Domain"].ToString(),
                    Email = reader["Email"].ToString(),
                    DnsChallengeToken = reader["DnsChallengeToken"].ToString(),
                    SavePath = reader["SavePath"].ToString(),
                    Provider = reader["Provider"].ToString(),
                    CreationDate = DateTime.Parse(reader["CreationTime"].ToString() ?? DateTime.MinValue.ToString()),
                    ExpiryDate = DateTime.Parse(reader["ExpiryDate"].ToString() ?? DateTime.MinValue.ToString()),
                    UseSeparateFiles = Convert.ToBoolean(reader["UseSeparateFiles"]),
                    SaveForRenewal = Convert.ToBoolean(reader["SaveForRenewal"]),
                    autoRenew = Convert.ToBoolean(reader["AutoRenew"]),
                    FailedRenewals = Convert.ToInt32(reader["FailedRenewals"]),
                    SuccessfulRenewals = Convert.ToInt32(reader["SuccessfulRenewals"]),
                    Signer = reader["Signer"].ToString(),
                    AccountID = reader["AccountID"].ToString(),
                    OrderUrl = reader["OrderUrl"].ToString(),
                    ChallengeType = reader["ChallengeType"].ToString(),
                    Thumbprint = reader["Thumbprint"].ToString(),
                    ZoneId = reader["ZoneId"].ToString()

                };

                records.Add(record);
            }

            return records;
        }

        public static async Task DeleteAllExpiredCerts()
        {
            using var connection = new SqliteConnection($"Data Source={Spheressl.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM ExpiredCerts;";
            await command.ExecuteNonQueryAsync();
            await ClearExpiredCertCountInDB();
        }



        //DNSProvider Management
        public async Task<bool> InsertDNSProvider(DNSProvider provider)
        {
            using var connection = new SqliteConnection($"Data Source={Spheressl.dbPath}");
            await connection.OpenAsync();
            try
            {
                var command = connection.CreateCommand();
                command.CommandText = @"
                INSERT INTO DNSProviders (
                    ProviderName,
                    Provider,
                    APIKey,
                    Ttl
                ) VALUES (
                @ProviderName,
                @Provider,
                @APIKey,
                @Ttl
                );
                ";

                command.Parameters.AddWithValue("@ProviderName", provider.ProviderName);
                command.Parameters.AddWithValue("@Provider", provider.Provider);
                command.Parameters.AddWithValue("@APIKey", provider.APIKey);
                command.Parameters.AddWithValue("@Ttl", provider.Ttl);

                await command.ExecuteNonQueryAsync();
                await AdjustTotalDNSProvidersInDB(1);


                if (!Spheressl.DNSProviders.Any(r => r.ProviderName == provider.ProviderName))
                {
                    Spheressl.DNSProviders.Add(provider);
                }
               
                return true;
            }
            catch (Exception ex)
            {
                _= _logger.Error("Failed to insert DNS provider: " + ex.Message);
                return false;

            }
        }

        public static async Task UpdateDNSProvider(DNSProvider updated)
        {
            using var connection = new SqliteConnection($"Data Source={Spheressl.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
        UPDATE DNSProviders
        SET 
            Provider = @Provider,
            APIKey = @APIKey,
            Ttl = @Ttl,
        WHERE ProviderName = @ProviderName;
        ";
         
            command.Parameters.AddWithValue("@ProviderName", updated.ProviderName);
            command.Parameters.AddWithValue("@Provider", updated.Provider);
            command.Parameters.AddWithValue("@APIKey", updated.APIKey);
            command.Parameters.AddWithValue("@Ttl", updated.Ttl);


            await command.ExecuteNonQueryAsync();
        }

        public static async Task DeleteDNSProviderByName(string providerName)
        {
            using var connection = new SqliteConnection($"Data Source={Spheressl.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
        DELETE FROM DNSProviders
        WHERE ProviderName = @ProviderName;
    ";

            command.Parameters.AddWithValue("@ProviderName", providerName);

            await command.ExecuteNonQueryAsync();
            await AdjustTotalDNSProvidersInDB(-1);

            var recordToRemove = Spheressl.DNSProviders.FirstOrDefault(r => r.ProviderName == providerName);
            if (recordToRemove != null)
            {
                Spheressl.DNSProviders.Remove(recordToRemove);
            }
        }

        public static async Task<DNSProvider?> GetDNSProviderByName(string name)
        {
            using var connection = new SqliteConnection($"Data Source={Spheressl.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
            SELECT ProviderName, Provider, APIKey, Ttl
            FROM DNSProviders
            WHERE ProviderName = @ProviderName;
                ";
            command.Parameters.AddWithValue("@ProviderName", name);

            using var reader = await command.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                return new DNSProvider
                {
                    ProviderName = reader["ProviderName"].ToString(),
                    Provider = reader["Provider"].ToString(),
                    APIKey = reader["APIKey"].ToString(),
                    Ttl = Convert.ToInt32(reader["Ttl"]),

                };
            }

            return null;
        }

        public static async Task<List<DNSProvider>> GetAllDNSProviders()
        {
            var providers = new List<DNSProvider>();

            using var connection = new SqliteConnection($"Data Source={Spheressl.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
            SELECT ProviderName, Provider, APIKey, Ttl
            FROM DNSProviders;
            ";

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var provider = new DNSProvider
                {
                    ProviderName = reader["ProviderName"].ToString(),
                    Provider = reader["Provider"].ToString(),
                    APIKey = reader["APIKey"].ToString(),
                    Ttl = Convert.ToInt32(reader["Ttl"]),

                };

                providers.Add(provider);
            }

            return providers;
        }

        public static async Task DeleteAllDNSProviders()
        {
            using var connection = new SqliteConnection($"Data Source={Spheressl.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM DNSProviders;";
            await command.ExecuteNonQueryAsync();
            await ClearTotalDNSProvidersInDB();
        }



        //Health
        public static async Task<HealthStat?> GetHealthStat()
        {
            using var connection = new SqliteConnection($"Data Source={Spheressl.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
        SELECT TotalCertsInDB, ExpiredCertCount, TotalDNSProviderCount, DateLastBooted
        FROM Health
        WHERE Id = 1;
    ";

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new HealthStat
                {
                    TotalCertsInDB = reader.GetInt32(0),
                    ExpiredCertCount = reader.GetInt32(1),
                    TotalDNSProviderCount = reader.GetInt32(2),
                    DateLastBooted = reader.GetString(3)
                };
            }

            return null;
        }

        public static async Task UpsertHealthStat(HealthStat stat)
        {
            using var connection = new SqliteConnection($"Data Source={Spheressl.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
        INSERT INTO Health (Id, TotalCertsInDB, ExpiredCertCount, TotalDNSProviderCount, DateLastBooted)
        VALUES (1, @TotalCertsInDB, @ExpiredCertCount, @TotalDNSProviderCount, @DateLastBooted)
        ON CONFLICT(Id) DO UPDATE SET
            TotalCertsInDB = excluded.TotalCertsInDB,
            ExpiredCertCount = excluded.ExpiredCertCount,
            TotalDNSProviderCount = excluded.TotalDNSProviderCount,
            DateLastBooted = excluded.DateLastBooted;
    ";

            command.Parameters.AddWithValue("@TotalCertsInDB", stat.TotalCertsInDB);
            command.Parameters.AddWithValue("@ExpiredCertCount", stat.ExpiredCertCount);
            command.Parameters.AddWithValue("@TotalDNSProviderCount", stat.TotalDNSProviderCount);
            command.Parameters.AddWithValue("@DateLastBooted", stat.DateLastBooted);

            await command.ExecuteNonQueryAsync();
        }

        public static async Task AdjustTotalCertsInDB(int delta)
        {
            if (delta != 1 && delta != -1) return;

            using var connection = new SqliteConnection($"Data Source={Spheressl.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
            UPDATE Health
            SET TotalCertsInDB = TotalCertsInDB + @Delta
            WHERE Id = 1;
            ";

            command.Parameters.AddWithValue("@Delta", delta);
            await command.ExecuteNonQueryAsync();
        }

        public static async Task ClearTotalCertsInDB()
        {
            using var connection = new SqliteConnection($"Data Source={Spheressl.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
            UPDATE Health
            SET TotalCertsInDB = 0
            WHERE Id = 1;
            ";

            await command.ExecuteNonQueryAsync();
        }

        public static async Task AdjustTotalDNSProvidersInDB(int delta)
        {
            if (delta != 1 && delta != -1) return;

            using var connection = new SqliteConnection($"Data Source={Spheressl.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
            UPDATE Health
            SET TotalDNSProviderCount = TotalDNSProviderCount + @Delta
            WHERE Id = 1;
             ";

            command.Parameters.AddWithValue("@Delta", delta);
            await command.ExecuteNonQueryAsync();
        }

        public static async Task ClearTotalDNSProvidersInDB()
        {
            using var connection = new SqliteConnection($"Data Source={Spheressl.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
            UPDATE Health
            SET TotalDNSProviderCount = 0
            WHERE Id = 1;
            ";

            await command.ExecuteNonQueryAsync();
        }

        public static async Task AdjustExpiredCertCountInDB(int delta)
        {
            if (delta != 1 && delta != -1) return;

            using var connection = new SqliteConnection($"Data Source={Spheressl.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
            UPDATE Health
            SET ExpiredCertCount = ExpiredCertCount + @Delta
            WHERE Id = 1;
             ";

            command.Parameters.AddWithValue("@Delta", delta);
            await command.ExecuteNonQueryAsync();
        }

        public static async Task ClearExpiredCertCountInDB()
        {
            using var connection = new SqliteConnection($"Data Source={Spheressl.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
            UPDATE Health
            SET ExpiredCertCount = 0
            WHERE Id = 1;
            ";

            await command.ExecuteNonQueryAsync();
        }

        public static async Task<int> GetTotalCertsInDB()
        {
            using var connection = new SqliteConnection($"Data Source={Spheressl.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT TotalCertsInDB FROM Health WHERE Id = 1";

            var result = await command.ExecuteScalarAsync();
            return result is DBNull or null ? 0 : Convert.ToInt32(result);
        }

        public static async Task<int> GetExpiredCertCount()
        {
            using var connection = new SqliteConnection($"Data Source={Spheressl.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT ExpiredCertCount FROM Health WHERE Id = 1";

            var result = await command.ExecuteScalarAsync();
            return result is DBNull or null ? 0 : Convert.ToInt32(result);
        }

        public static async Task<int> GetTotalDNSProviderCount()
        {
            using var connection = new SqliteConnection($"Data Source={Spheressl.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT TotalDNSProviderCount FROM Health WHERE Id = 1";

            var result = await command.ExecuteScalarAsync();
            return result is DBNull or null ? 0 : Convert.ToInt32(result);
        }

        public static async Task<string> GetDateLastBooted()
        {
            using var connection = new SqliteConnection($"Data Source={Spheressl.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT DateLastBooted FROM Health WHERE Id = 1";

            var result = await command.ExecuteScalarAsync();
            return result is DBNull or null ? "" : result.ToString();
        }

        public static async Task SetDateLastBooted(string timestamp)
        {
            using var connection = new SqliteConnection($"Data Source={Spheressl.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "UPDATE Health SET DateLastBooted = @Timestamp WHERE Id = 1";
            command.Parameters.AddWithValue("@Timestamp", timestamp);
            await command.ExecuteNonQueryAsync();
        }

        public static async Task RecalculateHealthStats()
        {
            using var connection = new SqliteConnection($"Data Source={Spheressl.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();

            command.CommandText = @"
        SELECT COUNT(*) FROM CertRecords;
    ";
            var totalCerts = Convert.ToInt32(await command.ExecuteScalarAsync());

            command.CommandText = @"
        SELECT COUNT(*) FROM CertRecords WHERE ExpiryDate < @Now;
    ";
            command.Parameters.Clear();
            command.Parameters.AddWithValue("@Now", DateTime.UtcNow.ToString("o"));
            var expiredCerts = Convert.ToInt32(await command.ExecuteScalarAsync());

            command.CommandText = @"
        SELECT COUNT(*) FROM DNSProviders;
    ";
            command.Parameters.Clear();
            var totalDNS = Convert.ToInt32(await command.ExecuteScalarAsync());

            command.CommandText = @"
        UPDATE Health
        SET TotalCertsInDB = @TotalCerts,
            ExpiredCertCount = @ExpiredCerts,
            TotalDNSProviderCount = @TotalDNS,
            DateLastBooted = @Now
        WHERE Id = 1;
    ";
            command.Parameters.Clear();
            command.Parameters.AddWithValue("@TotalCerts", totalCerts);
            command.Parameters.AddWithValue("@ExpiredCerts", expiredCerts);
            command.Parameters.AddWithValue("@TotalDNS", totalDNS);
            command.Parameters.AddWithValue("@Now", DateTime.UtcNow.ToString("o"));

            await command.ExecuteNonQueryAsync();
        }


        //DB Version and Migration
        public static async Task<int> GetDatabaseVersion()
        {
            using var connection = new SqliteConnection($"Data Source={Spheressl.dbPath}");
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
                using var connection = new SqliteConnection($"Data Source={Spheressl.dbPath}");
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
                using var connection = new SqliteConnection($"Data Source={Spheressl.dbPath}");
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
                using var connection = new SqliteConnection($"Data Source={Spheressl.dbPath}");
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
                using var connection = new SqliteConnection($"Data Source={Spheressl.dbPath}");
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
                using var connection = new SqliteConnection($"Data Source={Spheressl.dbPath}");
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
