using Microsoft.Data.Sqlite;
using SphereSSLv2.Models.DNSModels;
using SphereSSLv2.Services.Config;

namespace SphereSSLv2.Data.Database
{
    public class DnsProviderRepository
    {
        private DatabaseManager _databaseManager;

        //DNSProvider Management
        public async Task<bool> InsertDNSProvider(DNSProvider provider, string userId)
        {
            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
            await connection.OpenAsync();
            try
            {
                var command = connection.CreateCommand();
                command.CommandText = @"
            INSERT INTO DNSProviders (
                UserId,
                ProviderName,
                Provider,
                APIKey,
                Ttl
            ) VALUES (
                @UserId,
                @ProviderName,
                @Provider,
                @APIKey,
                @Ttl
            );
        ";

                command.Parameters.AddWithValue("@UserId", userId);
                command.Parameters.AddWithValue("@ProviderName", provider.ProviderName);
                command.Parameters.AddWithValue("@Provider", provider.Provider);
                command.Parameters.AddWithValue("@APIKey", provider.APIKey);
                command.Parameters.AddWithValue("@Ttl", provider.Ttl);

                await command.ExecuteNonQueryAsync();
                await HealthRepository.AdjustTotalDNSProvidersInDB(1);

                if (!ConfigureService.DNSProviders.Any(r => r.ProviderName == provider.ProviderName && r.UserId == userId))
                {
                    provider.UserId= userId;
                    ConfigureService.DNSProviders.Add(provider);
                }

                return true;
            }
            catch (Exception ex)
            {
                _ = _databaseManager._logger.Error("Failed to insert DNS provider: " + ex.Message);
                return false;
            }
        }

        public static async Task UpdateDNSProvider(DNSProvider updated, string userId)
        {
            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
            UPDATE DNSProviders
            SET 
            Provider = @Provider,
            APIKey = @APIKey,
            Ttl = @Ttl
            WHERE ProviderName = @ProviderName AND UserId = @UserId;
            ";

            command.Parameters.AddWithValue("@ProviderName", updated.ProviderName);
            command.Parameters.AddWithValue("@Provider", updated.Provider);
            command.Parameters.AddWithValue("@APIKey", updated.APIKey);
            command.Parameters.AddWithValue("@Ttl", updated.Ttl);
            command.Parameters.AddWithValue("@UserId", userId);

            await command.ExecuteNonQueryAsync();
        }

        public static async Task DeleteDNSProviderByName(string providerName, string userId)
        {
            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
            DELETE FROM DNSProviders
            WHERE ProviderName = @ProviderName AND UserId = @UserId;
            ";

            command.Parameters.AddWithValue("@ProviderName", providerName);
            command.Parameters.AddWithValue("@UserId", userId);

            await command.ExecuteNonQueryAsync();
            await HealthRepository.AdjustTotalDNSProvidersInDB(-1);

            var recordToRemove = ConfigureService.DNSProviders.FirstOrDefault(r => r.ProviderName == providerName && r.UserId == userId);
            if (recordToRemove != null)
            {
                ConfigureService.DNSProviders.Remove(recordToRemove);
            }
        }

        public static async Task<DNSProvider?> GetDNSProviderByName(string name, string userId)
        {
            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
            SELECT ProviderName, Provider, APIKey, Ttl, UserId
                FROM DNSProviders
            WHERE ProviderName = @ProviderName AND UserId = @UserId;
            ";
            command.Parameters.AddWithValue("@ProviderName", name);
            command.Parameters.AddWithValue("@UserId", userId);

            using var reader = await command.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                return new DNSProvider
                {
                    ProviderName = reader["ProviderName"].ToString(),
                    Provider = reader["Provider"].ToString(),
                    APIKey = reader["APIKey"].ToString(),
                    Ttl = Convert.ToInt32(reader["Ttl"]),
                    UserId = reader["UserId"].ToString()
                };
            }

            return null;
        }

        public static async Task<List<DNSProvider>> GetAllDNSProvidersByUserId(string userId)
        {
            var providers = new List<DNSProvider>();

            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
            SELECT ProviderName, Provider, APIKey, Ttl, UserId
            FROM DNSProviders
            WHERE UserId = @UserId;
            ";
            command.Parameters.AddWithValue("@UserId", userId);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var provider = new DNSProvider
                {
                    ProviderName = reader["ProviderName"].ToString(),
                    Provider = reader["Provider"].ToString(),
                    APIKey = reader["APIKey"].ToString(),
                    Ttl = Convert.ToInt32(reader["Ttl"]),
                    UserId = reader["UserId"].ToString()
                };

                providers.Add(provider);
            }

            return providers;
        }

        public static async Task DeleteAllDNSProviders(string userId)
        {
            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM DNSProviders WHERE UserId = @UserId;";
            command.Parameters.AddWithValue("@UserId", userId);
            await command.ExecuteNonQueryAsync();
            await HealthRepository.ClearTotalDNSProvidersInDB();
        }

        public static async Task<List<DNSProvider>> GetAllDNSProviders()
        {
            var providers = new List<DNSProvider>();

            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
        SELECT UserId, ProviderName, Provider, APIKey, Ttl
        FROM DNSProviders;
    ";

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var provider = new DNSProvider
                {
                    UserId = reader["UserId"].ToString(),
                    ProviderName = reader["ProviderName"].ToString(),
                    Provider = reader["Provider"].ToString(),
                    APIKey = reader["APIKey"].ToString(),
                    Ttl = Convert.ToInt32(reader["Ttl"]),
                };

                providers.Add(provider);
            }

            return providers;
        }
    }
}
