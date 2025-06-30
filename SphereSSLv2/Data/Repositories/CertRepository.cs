using Microsoft.Data.Sqlite;
using SphereSSLv2.Models.CertModels;
using SphereSSLv2.Services.Config;

namespace SphereSSLv2.Data.Repositories
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
            UserId, OrderId, Domain, Email, DnsChallengeToken, SavePath, Provider,
            CreationTime, ExpiryDate, UseSeparateFiles, SaveForRenewal, AutoRenew,
            FailedRenewals, SuccessfulRenewals, Signer, AccountID, OrderUrl,
            ChallengeType, Thumbprint, ZoneId
        )
        VALUES (
            @UserId, @OrderId, @Domain, @Email, @DnsChallengeToken, @SavePath, @Provider,
            @CreationTime, @ExpiryDate, @UseSeparateFiles, @SaveForRenewal, @AutoRenew,
            @FailedRenewals, @SuccessfulRenewals, @Signer, @AccountID, @OrderUrl,
            @ChallengeType, @Thumbprint, @ZoneId
        );";

            command.Parameters.AddWithValue("@UserId", record.UserId);
            command.Parameters.AddWithValue("@OrderId", record.OrderId);
            command.Parameters.AddWithValue("@Domain", record.Domains);
            command.Parameters.AddWithValue("@Email", record.Email);
            command.Parameters.AddWithValue("@DnsChallengeToken", record.DnsChallengeToken);
            command.Parameters.AddWithValue("@SavePath", record.SavePath);
            command.Parameters.AddWithValue("@Provider", record.ProviderId);
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
            command.Parameters.AddWithValue("@Email", record.Email);
            command.Parameters.AddWithValue("@SavePath", record.SavePath);
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


            foreach (var challenge in record.Challanges)
            {

                await UpdateAcmeChallengeAsync(challenge);

            }
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
                    UserId = reader["UserId"].ToString(),
                    OrderId = reader["OrderId"].ToString(),
                    Challanges = await GetAllCertsForOrderIdAsync(reader["OrderId"].ToString()),
                    Email = reader["Email"].ToString(),          
                    SavePath = reader["SavePath"].ToString(),
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
                    UserId = reader["UserId"].ToString(),
                    OrderId = reader["OrderId"].ToString(),
                    Domains = reader["Domain"].ToString(),
                    Email = reader["Email"].ToString(),
                    DnsChallengeToken = reader["DnsChallengeToken"].ToString(),
                    SavePath = reader["SavePath"].ToString(),
                    ProviderId = reader["Provider"].ToString(),
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
            UserId, OrderId, Domain, Email, DnsChallengeToken, SavePath, Provider,
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
                    UserId = reader["UserId"].ToString(),
                    OrderId = reader["OrderId"].ToString(),
                    Domains = reader["Domain"].ToString(),
                    Email = reader["Email"].ToString(),
                    DnsChallengeToken = reader["DnsChallengeToken"].ToString(),
                    SavePath = reader["SavePath"].ToString(),
                    ProviderId = reader["Provider"].ToString(),
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
                    UserId = reader["UserId"].ToString(),
                    OrderId = reader["OrderId"].ToString(),
                    Domains = reader["Domain"].ToString(),
                    Email = reader["Email"].ToString(),
                    DnsChallengeToken = reader["DnsChallengeToken"].ToString(),
                    SavePath = reader["SavePath"].ToString(),
                    ProviderId = reader["Provider"].ToString(),
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
                    UserId = reader["UserId"].ToString(),
                    OrderId = reader["OrderId"].ToString(),
                    Domains = reader["Domain"].ToString(),
                    Email = reader["Email"].ToString(),
                    DnsChallengeToken = reader["DnsChallengeToken"].ToString(),
                    SavePath = reader["SavePath"].ToString(),
                    ProviderId = reader["Provider"].ToString(),
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
                    UserId = reader.GetString(0),
                    OrderId = reader.GetString(1),
                    Domains = reader.GetString(2),
                    Email = reader.GetString(3),
                    DnsChallengeToken = reader.IsDBNull(4) ? "" : reader.GetString(4),
                    SavePath = reader.IsDBNull(5) ? "" : reader.GetString(5),
                    ProviderId = reader.IsDBNull(6) ? "" : reader.GetString(6),
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

        public async Task<List<CertRecord>> GetAllCertsForUserIdAsync(string userId)
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
                    Domains = reader["Domain"].ToString(),
                    Email = reader["Email"].ToString(),
                    DnsChallengeToken = reader["DnsChallengeToken"]?.ToString(),
                    SavePath = reader["SavePath"]?.ToString(),
                    ProviderId = reader["Provider"]?.ToString(),
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




        // AcmeChallenge Management
        public static async Task<List<AcmeChallenge>> GetAllCertsForOrderIdAsync(string orderId)
        {
            var challenges = new List<AcmeChallenge>();

            // Replace with your actual connection string
            using var conn = new SqliteConnection("Data Source=your_database_path.db");
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
           cmd.CommandText = @"
            SELECT ChallengeId, OrderId, UserId, Domain, ChallengeToken, ProviderId, Status
            FROM Challenges
            WHERE OrderId = @OrderId";
            cmd.Parameters.AddWithValue("@OrderId", orderId);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                challenges.Add(new AcmeChallenge
                {
                    ChallangeId = reader["ChallengeId"]?.ToString() ?? string.Empty,
                    OrderId = reader["OrderId"]?.ToString() ?? string.Empty,
                    UserId = reader["UserId"]?.ToString() ?? string.Empty,
                    Domain = reader["Domain"]?.ToString() ?? string.Empty,
                    DnsChallengeToken = reader["ChallengeToken"]?.ToString() ?? string.Empty,
                    ProviderId = reader["ProviderId"]?.ToString() ?? string.Empty,
                    Status = reader["Status"]?.ToString() ?? string.Empty
                });
            }

            return challenges;
        }

        public static async Task UpdateAcmeChallengeAsync(AcmeChallenge challenge)
        {
            // Replace with your actual connection string
            using var conn = new SqliteConnection("Data Source=your_database_path.db");
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
        UPDATE Challenges SET
            OrderId = @OrderId,
            UserId = @UserId,
            Domain = @Domain,
            ChallengeToken = @ChallengeToken,
            ProviderId = @ProviderId,
            Status = @Status
        WHERE ChallengeId = @ChallengeId";

            cmd.Parameters.AddWithValue("@OrderId", challenge.OrderId ?? string.Empty);
            cmd.Parameters.AddWithValue("@UserId", challenge.UserId ?? string.Empty);
            cmd.Parameters.AddWithValue("@Domain", challenge.Domain ?? string.Empty);
            cmd.Parameters.AddWithValue("@ChallengeToken", challenge.DnsChallengeToken ?? string.Empty);
            cmd.Parameters.AddWithValue("@ProviderId", challenge.ProviderId ?? string.Empty);
            cmd.Parameters.AddWithValue("@Status", challenge.Status ?? string.Empty);
            cmd.Parameters.AddWithValue("@ChallengeId", challenge.ChallangeId ?? string.Empty);

            await cmd.ExecuteNonQueryAsync();
        }

    }
}
