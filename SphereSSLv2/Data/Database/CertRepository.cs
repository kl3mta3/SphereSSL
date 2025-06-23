using Microsoft.Data.Sqlite;
using SphereSSLv2.Models.CertModels;
using SphereSSLv2.Services.Config;

namespace SphereSSLv2.Data.Database
{
    public class CertRepository
    {



        //CertRecord Management
        public static async Task InsertCertRecord(CertRecord record)
        {


            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
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
            await HealthRepository.AdjustTotalCertsInDB(1);

            if (!ConfigureService.CertRecords.Any(r => r.OrderId == record.OrderId))
            {
                ConfigureService.CertRecords.Add(record);


            }
        }

        public static async Task UpdateCertRecord(CertRecord record)
        {
            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
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
                connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
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

            await HealthRepository.AdjustTotalCertsInDB(-1);

            var recordToRemove = ConfigureService.CertRecords.FirstOrDefault(r => r.OrderId == orderId);
            if (recordToRemove != null)
            {
                ConfigureService.CertRecords.Remove(recordToRemove);
            }
        }

        public static async Task<CertRecord?> GetCertRecordByOrderId(string orderId)
        {
            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
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
            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
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

            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
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
            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM CertRecords;";
            await command.ExecuteNonQueryAsync();
            await HealthRepository.ClearTotalCertsInDB();
        }

        public static async Task<List<CertRecord>> GetExpiringSoonCerts(int daysThreshold = 30)
        {
            var records = new List<CertRecord>();

            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
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

            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
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
            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
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

            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();

            foreach (CertRecord record in certList)
            {
                await InsertExpiredCert(record, connection, transaction);
                await DeleteCertRecordByOrderId(record.OrderId, connection, transaction);
            }

            await transaction.CommitAsync();
        }

        public async Task<List<CertRecord>> GetAllCertsForUserAsync(string userId)
        {
            var certs = new List<CertRecord>();

            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
        SELECT 
            Id, UserId, OrderId, Domain, Email, DnsChallengeToken, SavePath, Provider, 
            CreationTime, ExpiryDate, UseSeparateFiles, SaveForRenewal, AutoRenew, 
            FailedRenewals, SuccessfulRenewals, Signer, AccountID, OrderUrl, 
            ChallengeType, Thumbprint, ZoneId
        FROM CertRecords
        WHERE UserId = @UserId;
    ";

            command.Parameters.AddWithValue("@UserId", userId);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                certs.Add(new CertRecord
                {
                    UserId = reader["UserId"].ToString(),
                    OrderId = reader["OrderId"].ToString(),
                    Domain = reader["Domain"].ToString(),
                    Email = reader["Email"].ToString(),
                    DnsChallengeToken = reader["DnsChallengeToken"]?.ToString(),
                    SavePath = reader["SavePath"]?.ToString(),
                    Provider = reader["Provider"]?.ToString(),
                    CreationDate = DateTime.Parse(reader["CreationTime"].ToString()),
                    ExpiryDate = DateTime.Parse(reader["ExpiryDate"].ToString()),
                    UseSeparateFiles = reader.GetInt32(reader.GetOrdinal("UseSeparateFiles")) == 1,
                    SaveForRenewal = reader.GetInt32(reader.GetOrdinal("SaveForRenewal")) == 1,
                    autoRenew = reader.GetInt32(reader.GetOrdinal("AutoRenew")) == 1,
                    FailedRenewals = reader.GetInt32(reader.GetOrdinal("FailedRenewals")),
                    SuccessfulRenewals = reader.GetInt32(reader.GetOrdinal("SuccessfulRenewals")),
                    Signer = reader["Signer"]?.ToString(),
                    AccountID = reader["AccountID"]?.ToString(),
                    OrderUrl = reader["OrderUrl"]?.ToString(),
                    ChallengeType = reader["ChallengeType"]?.ToString(),
                    Thumbprint = reader["Thumbprint"]?.ToString(),
                    ZoneId = reader["ZoneId"]?.ToString()
                });
            }

            return certs;
        }


        //Expired cert Management
        public static async Task InsertExpiredCert(CertRecord record, SqliteConnection? connection = null, SqliteTransaction? transaction = null)
        {
            if (connection == null)
            {
                connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
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
            await HealthRepository.AdjustExpiredCertCountInDB(1);

            if (!ConfigureService.ExpiredCertRecords.Any(r => r.OrderId == record.OrderId))
            {
                ConfigureService.ExpiredCertRecords.Add(record);
            }

        }

        public static async Task DeleteExpiredCertsByOrderId(string orderId)
        {
            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
            DELETE FROM ExpiredCerts
            WHERE OrderId = @OrderId;
            ";

            command.Parameters.AddWithValue("@OrderId", orderId);

            await command.ExecuteNonQueryAsync();
            await HealthRepository.AdjustExpiredCertCountInDB(-1);

            var recordToRemove = ConfigureService.ExpiredCertRecords.FirstOrDefault(r => r.OrderId == orderId);
            if (recordToRemove != null)
            {
                ConfigureService.ExpiredCertRecords.Remove(recordToRemove);
            }

        }

        public static async Task<CertRecord?> GetExpiredCertByOrderId(string orderId)
        {
            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
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
            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
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

            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
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
            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM ExpiredCerts;";
            await command.ExecuteNonQueryAsync();
            await HealthRepository.ClearExpiredCertCountInDB();
        }

        public async Task<List<CertRecord>> GetAllExpiredCertsAsync()
        {
            var certs = new List<CertRecord>();

            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
        SELECT 
            Id, UserId, OrderId, Domain, Email, DnsChallengeToken, SavePath, Provider,
            CreationTime, ExpiryDate, UseSeparateFiles, SaveForRenewal, AutoRenew,
            FailedRenewals, SuccessfulRenewals, Signer, AccountID, OrderUrl,
            ChallengeType, Thumbprint, ZoneId
        FROM ExpiredCerts;
    ";

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                certs.Add(new CertRecord
                {
                    UserId = reader["UserId"].ToString(),
                    OrderId = reader["OrderId"].ToString(),
                    Domain = reader["Domain"].ToString(),
                    Email = reader["Email"].ToString(),
                    DnsChallengeToken = reader["DnsChallengeToken"]?.ToString(),
                    SavePath = reader["SavePath"]?.ToString(),
                    Provider = reader["Provider"]?.ToString(),
                    CreationDate = DateTime.Parse(reader["CreationTime"].ToString()),
                    ExpiryDate = DateTime.Parse(reader["ExpiryDate"].ToString()),
                    UseSeparateFiles = reader.GetInt32(reader.GetOrdinal("UseSeparateFiles")) == 1,
                    SaveForRenewal = reader.GetInt32(reader.GetOrdinal("SaveForRenewal")) == 1,
                    autoRenew = reader.GetInt32(reader.GetOrdinal("AutoRenew")) == 1,
                    FailedRenewals = reader.GetInt32(reader.GetOrdinal("FailedRenewals")),
                    SuccessfulRenewals = reader.GetInt32(reader.GetOrdinal("SuccessfulRenewals")),
                    Signer = reader["Signer"]?.ToString(),
                    AccountID = reader["AccountID"]?.ToString(),
                    OrderUrl = reader["OrderUrl"]?.ToString(),
                    ChallengeType = reader["ChallengeType"]?.ToString(),
                    Thumbprint = reader["Thumbprint"]?.ToString(),
                    ZoneId = reader["ZoneId"]?.ToString()
                });
            }

            return certs;
        }

        public async Task<List<CertRecord>> GetExpiredCertsByUserIdAsync(string userId)
        {
            var certs = new List<CertRecord>();

            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
        SELECT 
            Id, UserId, OrderId, Domain, Email, DnsChallengeToken, SavePath, Provider,
            CreationTime, ExpiryDate, UseSeparateFiles, SaveForRenewal, AutoRenew,
            FailedRenewals, SuccessfulRenewals, Signer, AccountID, OrderUrl,
            ChallengeType, Thumbprint, ZoneId
        FROM ExpiredCerts
        WHERE UserId = @UserId;
    ";
            command.Parameters.AddWithValue("@UserId", userId);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                certs.Add(new CertRecord
                {
                    UserId = reader["UserId"].ToString(),
                    OrderId = reader["OrderId"].ToString(),
                    Domain = reader["Domain"].ToString(),
                    Email = reader["Email"].ToString(),
                    DnsChallengeToken = reader["DnsChallengeToken"]?.ToString(),
                    SavePath = reader["SavePath"]?.ToString(),
                    Provider = reader["Provider"]?.ToString(),
                    CreationDate = DateTime.Parse(reader["CreationTime"].ToString()),
                    ExpiryDate = DateTime.Parse(reader["ExpiryDate"].ToString()),
                    UseSeparateFiles = reader.GetInt32(reader.GetOrdinal("UseSeparateFiles")) == 1,
                    SaveForRenewal = reader.GetInt32(reader.GetOrdinal("SaveForRenewal")) == 1,
                    autoRenew = reader.GetInt32(reader.GetOrdinal("AutoRenew")) == 1,
                    FailedRenewals = reader.GetInt32(reader.GetOrdinal("FailedRenewals")),
                    SuccessfulRenewals = reader.GetInt32(reader.GetOrdinal("SuccessfulRenewals")),
                    Signer = reader["Signer"]?.ToString(),
                    AccountID = reader["AccountID"]?.ToString(),
                    OrderUrl = reader["OrderUrl"]?.ToString(),
                    ChallengeType = reader["ChallengeType"]?.ToString(),
                    Thumbprint = reader["Thumbprint"]?.ToString(),
                    ZoneId = reader["ZoneId"]?.ToString()
                });
            }

            return certs;
        }





    }
}
