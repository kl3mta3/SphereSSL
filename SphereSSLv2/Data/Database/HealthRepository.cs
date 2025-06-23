using Microsoft.Data.Sqlite;
using SphereSSLv2.Models.ConfigModels;
using SphereSSLv2.Services.Config;

namespace SphereSSLv2.Data.Database
{
    public class HealthRepository
    {


        //Health
        public static async Task<HealthStat?> GetHealthStat()
        {
            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
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

        public static async Task InsertHealthStat(HealthStat stat)
        {
            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
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

            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
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
            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
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

            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
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
            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
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

            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
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
            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
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
            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT TotalCertsInDB FROM Health WHERE Id = 1";

            var result = await command.ExecuteScalarAsync();
            return result is DBNull or null ? 0 : Convert.ToInt32(result);
        }

        public static async Task<int> GetExpiredCertCount()
        {
            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT ExpiredCertCount FROM Health WHERE Id = 1";

            var result = await command.ExecuteScalarAsync();
            return result is DBNull or null ? 0 : Convert.ToInt32(result);
        }

        public static async Task<int> GetTotalDNSProviderCount()
        {
            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT TotalDNSProviderCount FROM Health WHERE Id = 1";

            var result = await command.ExecuteScalarAsync();
            return result is DBNull or null ? 0 : Convert.ToInt32(result);
        }

        public static async Task<string> GetDateLastBooted()
        {
            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT DateLastBooted FROM Health WHERE Id = 1";

            var result = await command.ExecuteScalarAsync();
            return result is DBNull or null ? "" : result.ToString();
        }

        public static async Task SetDateLastBooted(string timestamp)
        {
            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "UPDATE Health SET DateLastBooted = @Timestamp WHERE Id = 1";
            command.Parameters.AddWithValue("@Timestamp", timestamp);
            await command.ExecuteNonQueryAsync();
        }

        public static async Task RecalculateHealthStats()
        {
            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
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


    }
}
