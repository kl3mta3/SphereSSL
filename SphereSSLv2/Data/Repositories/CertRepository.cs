using Microsoft.Data.Sqlite;
using SphereSSLv2.Models.CertModels;
using SphereSSLv2.Services.Config;
using System.Security.AccessControl;

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
                UserId, OrderId, Email, SavePath, CreationTime, ExpiryDate, UseSeparateFiles, SaveForRenewal, AutoRenew,
                FailedRenewals, SuccessfulRenewals, Signer, AccountID, OrderUrl,
                ChallengeType, Thumbprint
            )
            VALUES (
                @UserId, @OrderId, @Email, @SavePath, @CreationTime, @ExpiryDate, @UseSeparateFiles, @SaveForRenewal, @AutoRenew,
                @FailedRenewals, @SuccessfulRenewals, @Signer, @AccountID, @OrderUrl,
                @ChallengeType, @Thumbprint
            );";

            command.Parameters.AddWithValue("@UserId", record.UserId);
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

            await command.ExecuteNonQueryAsync();

            foreach (var challenge in record.Challenges)
            {
                await InsertAcmeChallengeAsync(challenge);
            }


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
            Email = @Email,
            SavePath = @SavePath,
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
            Thumbprint = @Thumbprint

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
        

            await command.ExecuteNonQueryAsync();


            foreach (var challenge in record.Challenges)
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
                var cert = new CertRecord
                {
                    UserId = reader["UserId"].ToString(),
                    OrderId = reader["OrderId"].ToString(),
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
                    Challenges = new List<AcmeChallenge>()
                };


                cert.Challenges = await GetAllCertsForOrderIdAsync(cert.OrderId);
                return cert;

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
                UserId, OrderId, Email, SavePath, CreationTime, ExpiryDate, UseSeparateFiles, SaveForRenewal, AutoRenew,
                FailedRenewals, SuccessfulRenewals, Signer, AccountID, OrderUrl,
                ChallengeType, Thumbprint
            FROM CertRecords;
            ";

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var cert = new CertRecord
                {
                    UserId = reader["UserId"].ToString(),
                    OrderId = reader["OrderId"].ToString(),
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
                    Challenges = new List<AcmeChallenge>()
                };


                cert.Challenges = await GetAllCertsForOrderIdAsync(cert.OrderId);

                records.Add(cert);
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
                    Challenges = new List<AcmeChallenge>()
                };


                cert.Challenges = await GetAllCertsForOrderIdAsync(cert.OrderId);
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
                    Challenges = await GetAllCertsForOrderIdAsync(reader["OrderId"].ToString()),
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
                    Thumbprint = reader["Thumbprint"].ToString()
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
        SELECT * FROM CertRecords
        WHERE julianday(ExpiryDate) - julianday('now') <= 0;
    ";

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var record = new CertRecord
                {
                    UserId = reader["UserId"]?.ToString() ?? "",
                    OrderId = reader["OrderId"]?.ToString() ?? "",
                    Email = reader["Email"]?.ToString() ?? "",
                    SavePath = reader["SavePath"]?.ToString() ?? "",
                    Signer = reader["Signer"]?.ToString() ?? "",
                    AccountID = reader["AccountID"]?.ToString() ?? "",
                    OrderUrl = reader["OrderUrl"]?.ToString() ?? "",
                    ChallengeType = reader["ChallengeType"]?.ToString() ?? "",
                    Thumbprint = reader["Thumbprint"]?.ToString() ?? "",
                    CreationDate = DateTime.TryParse(reader["CreationTime"]?.ToString(), out var created) ? created : DateTime.MinValue,
                    ExpiryDate = DateTime.TryParse(reader["ExpiryDate"]?.ToString(), out var expired) ? expired : DateTime.MinValue,
                    UseSeparateFiles = Convert.ToInt32(reader["UseSeparateFiles"]) == 1,
                    SaveForRenewal = Convert.ToInt32(reader["SaveForRenewal"]) == 1,
                    autoRenew = Convert.ToInt32(reader["AutoRenew"]) == 1,
                    FailedRenewals = Convert.ToInt32(reader["FailedRenewals"]),
                    SuccessfulRenewals = Convert.ToInt32(reader["SuccessfulRenewals"]),
                    Challenges = new List<AcmeChallenge>(),
                };

               
                record.Challenges = await GetAllCertsForOrderIdAsync(record.OrderId);

                records.Add(record);
            }

            return records;
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
            ChallengeType, Thumbprint
        FROM CertRecords
        WHERE UserId = @UserId;
    ";

            command.Parameters.AddWithValue("@UserId", userId);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                CertRecord cert = new CertRecord
                {
                    UserId = reader["UserId"].ToString(),
                    OrderId = reader["OrderId"].ToString(),
                    Email = reader["Email"].ToString(),           
                    SavePath = reader["SavePath"]?.ToString(),
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
                    Challenges = new List<AcmeChallenge>()
                };

                cert.Challenges = await GetAllCertsForOrderIdAsync(cert.OrderId);
                certs.Add(cert);
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
            SELECT ChallengeId, OrderId, UserId, Domain, ChallengeToken, ProviderId, Status, ZoneId
            FROM Challenges
            WHERE OrderId = @OrderId";
            cmd.Parameters.AddWithValue("@OrderId", orderId);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                challenges.Add(new AcmeChallenge
                {
                    ChallengeId = reader["ChallengeId"]?.ToString() ?? string.Empty,
                    OrderId = reader["OrderId"]?.ToString() ?? string.Empty,
                    UserId = reader["UserId"]?.ToString() ?? string.Empty,
                    Domain = reader["Domain"]?.ToString() ?? string.Empty,
                    AuthorizationUrl = reader["AuthorizationUrl"]?.ToString() ?? string.Empty,
                    DnsChallengeToken = reader["ChallengeToken"]?.ToString() ?? string.Empty,
                    ProviderId = reader["ProviderId"]?.ToString() ?? string.Empty,
                    Status = reader["Status"]?.ToString() ?? string.Empty,
                    ZoneId= reader["ZoneId"]?.ToString() ?? string.Empty
                });
            }

            return challenges;
        }

        public static async Task UpdateAcmeChallengeAsync(AcmeChallenge challenge)
        {
            
            using var conn = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
            UPDATE Challenges SET
                OrderId = @OrderId,
                UserId = @UserId,
                Domain = @Domain,
                AuthorizationUrl = @AuthorizationUrl,
                ChallengeToken = @ChallengeToken,
                ProviderId = @ProviderId,
                Status = @Status,
                ZoneId = @ZoneId
            WHERE ChallengeId = @ChallengeId";

            cmd.Parameters.AddWithValue("@OrderId", challenge.OrderId ?? string.Empty);
            cmd.Parameters.AddWithValue("@UserId", challenge.UserId ?? string.Empty);
            cmd.Parameters.AddWithValue("@Domain", challenge.Domain ?? string.Empty);
            cmd.Parameters.AddWithValue("@AuthorizationUrl", challenge.AuthorizationUrl ?? string.Empty);
            cmd.Parameters.AddWithValue("@ChallengeToken", challenge.DnsChallengeToken ?? string.Empty);
            cmd.Parameters.AddWithValue("@ProviderId", challenge.ProviderId ?? string.Empty);
            cmd.Parameters.AddWithValue("@Status", challenge.Status ?? string.Empty);
            cmd.Parameters.AddWithValue("@ChallengeId", challenge.ChallengeId ?? string.Empty);
            cmd.Parameters.AddWithValue("@ZoneId", challenge.ZoneId ?? string.Empty);

            await cmd.ExecuteNonQueryAsync();
        }

        public static async Task InsertAcmeChallengeAsync(AcmeChallenge challenge)
        {
            using var conn = new SqliteConnection($"Data Source ={ ConfigureService.dbPath }");
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
            INSERT INTO Challenges (
                ChallengeId,
                OrderId,
                UserId,
                Domain,
                AuthorizationUrl,
                ChallengeToken,
                ProviderId,
                Status, 
                ZoneId
            )
            VALUES (
                @ChallengeId,
                @OrderId,
                @UserId,
                @Domain,
                @AuthorizationUrl,
                @ChallengeToken,
                @ProviderId,
                @Status,
                @ZoneId
            );
            ";

            cmd.Parameters.AddWithValue("@ChallengeId", challenge.ChallengeId ?? string.Empty);
            cmd.Parameters.AddWithValue("@OrderId", challenge.OrderId ?? string.Empty);
            cmd.Parameters.AddWithValue("@UserId", challenge.UserId ?? string.Empty);
            cmd.Parameters.AddWithValue("@Domain", challenge.Domain ?? string.Empty);
            cmd.Parameters.AddWithValue("@AuthorizationUrl", challenge.AuthorizationUrl ?? string.Empty);
            cmd.Parameters.AddWithValue("@ChallengeToken", challenge.DnsChallengeToken ?? string.Empty);
            cmd.Parameters.AddWithValue("@ProviderId", challenge.ProviderId ?? string.Empty);
            cmd.Parameters.AddWithValue("@Status", challenge.Status ?? string.Empty);
            cmd.Parameters.AddWithValue("@ZoneId", challenge.ZoneId ?? string.Empty);

            await cmd.ExecuteNonQueryAsync();
        }

    }
}
