using Microsoft.Data.Sqlite;
using SphereSSLv2.Data.Database;
using SphereSSLv2.Models.DNSModels;
using SphereSSLv2.Services.Config;


namespace SphereSSLv2.Data.Repositories
{
    public class DnsProviderRepository
    {
        private DatabaseManager _databaseManager;
        private UserRepository _userRepository;

        //DNSProvider Management
        public async Task<bool> InsertDNSProvider(DNSProvider provider, string userId)
        {
            if (_userRepository == null)
            {
                _userRepository = new UserRepository(this);
            }

            string username = await _userRepository.GetUsernameByIdAsync(userId);

            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
            await connection.OpenAsync();
            try
            {
                var command = connection.CreateCommand();
                command.CommandText = @"
            INSERT INTO DNSProviders (
                ProviderId,
                UserId,
                Username,
                ProviderName,
                Provider,
                APIKey,
                Ttl
            ) VALUES (
                @ProviderId,
                @UserId,
                @Username,
                @ProviderName,
                @Provider,
                @APIKey,
                @Ttl
            );
        ";
                command.Parameters.AddWithValue("@ProviderId", provider.ProviderId);
                command.Parameters.AddWithValue("@UserId", userId);
                command.Parameters.AddWithValue("@Username", username);
                command.Parameters.AddWithValue("@ProviderName", provider.ProviderName);
                command.Parameters.AddWithValue("@Provider", provider.Provider);
                command.Parameters.AddWithValue("@APIKey", provider.APIKey);
                command.Parameters.AddWithValue("@Ttl", provider.Ttl);

                await command.ExecuteNonQueryAsync();
                await HealthRepository.AdjustTotalDNSProvidersInDB(1);

                if (!ConfigureService.DNSProviders.Any(r => r.ProviderName == provider.ProviderName && r.UserId == userId))
                {
                    provider.UserId = userId;
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

        public async Task<bool> UpdateDNSProvider(DNSProvider updated)
        {

            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
            await connection.OpenAsync();
            try
            {
                var command = connection.CreateCommand();
                command.CommandText = @"
            UPDATE DNSProviders
            SET 
            ProviderName =@ProviderName,
            Provider = @Provider,
            APIKey = @APIKey,
            Username = @Username,
            Ttl = @Ttl
            WHERE ProviderId = @ProviderId;
            ";
                command.Parameters.AddWithValue("@ProviderId", updated.ProviderId);
                command.Parameters.AddWithValue("@ProviderName", updated.ProviderName);
                command.Parameters.AddWithValue("@Provider", updated.Provider);
                command.Parameters.AddWithValue("@APIKey", updated.APIKey);
                command.Parameters.AddWithValue("@Ttl", updated.Ttl);
                command.Parameters.AddWithValue("@Username", updated.Username);

                await command.ExecuteNonQueryAsync();
                return true;
            }
            catch
            {


                return false;
            }
        }

        public async Task DeleteDNSProviderByName(string providerName, string userId)
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

        public async Task<DNSProvider?> GetDNSProviderByName(string name, string userId)
        {


            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
            SELECT ProviderName, Provider,  ProviderId, APIKey, Ttl, UserId, Username
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
                    ProviderId = reader["ProviderId"].ToString(),
                    Username = reader["Username"].ToString(),
                    ProviderName = reader["ProviderName"].ToString(),
                    Provider = reader["Provider"].ToString(),
                    APIKey = reader["APIKey"].ToString(),
                    Ttl = Convert.ToInt32(reader["Ttl"]),
                    UserId = reader["UserId"].ToString()
                };
            }

            return null;
        }

        public async Task<List<DNSProvider>> GetAllDNSProvidersByUserId(string userId)
        {


            var providers = new List<DNSProvider>();

            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
            SELECT ProviderName,  ProviderId, Provider, APIKey, Ttl, UserId, Username
            FROM DNSProviders
            WHERE UserId = @UserId;
            ";
            command.Parameters.AddWithValue("@UserId", userId);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {

                var provider = new DNSProvider
                {
                    ProviderId = reader["ProviderId"].ToString(),
                    Username = reader["Username"].ToString(),
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

        public async Task DeleteAllDNSProvidersForUser(string userId)
        {
            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM DNSProviders WHERE UserId = @UserId;";
            command.Parameters.AddWithValue("@UserId", userId);
            await command.ExecuteNonQueryAsync();
            await HealthRepository.ClearTotalDNSProvidersInDB();
        }

        public async Task<List<DNSProvider>> GetAllDNSProviders()
        {
            var providers = new List<DNSProvider>();

            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
            SELECT UserId, Username, ProviderId, ProviderName, Provider, APIKey, Ttl
            FROM DNSProviders;
            ";

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var provider = new DNSProvider
                {
                    ProviderId = reader["ProviderId"].ToString(),
                    Username = reader["Username"].ToString(),
                    UserId = reader["UserId"].ToString(),
                    ProviderName = reader["ProviderName"].ToString(),
                    Provider = reader["Provider"].ToString(),
                    APIKey = reader["APIKey"].ToString(),
                    Ttl = reader["Ttl"] == DBNull.Value ? 0 : Convert.ToInt32(reader["Ttl"]),
                };

                providers.Add(provider);
            }

            return providers;
        }

        internal async Task<DNSProvider> GetDNSProviderById(string providerId)
        {

            var provider = new DNSProvider();

            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
            SELECT UserId, Username, ProviderId, ProviderName, Provider, APIKey, Ttl
            FROM DNSProviders
            WHERE ProviderId = @ProviderId;
            ";
            command.Parameters.AddWithValue("@ProviderId", providerId);
            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {

                provider = new DNSProvider
                {
                    ProviderId = reader["ProviderId"].ToString(),
                    Username = reader["Username"].ToString(),
                    UserId = reader["UserId"].ToString(),
                    ProviderName = reader["ProviderName"].ToString(),
                    Provider = reader["Provider"].ToString(),
                    APIKey = reader["APIKey"].ToString(),
                    Ttl = reader["Ttl"] == DBNull.Value ? 0 : Convert.ToInt32(reader["Ttl"]),
                };


                return provider;
            }
            else

            {
                return null;
            }




        }

        public async Task<bool> DeleteDNSProviderById(string providerId)
        {
            try
            {
                using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
                await connection.OpenAsync();

                var command = connection.CreateCommand();
                command.CommandText = @"
                DELETE FROM DNSProviders
                WHERE ProviderId = @ProviderId;
                "
                ;

                command.Parameters.AddWithValue("@ProviderId", providerId);

                await command.ExecuteNonQueryAsync();
                await HealthRepository.AdjustTotalDNSProvidersInDB(-1);
                return true;

            }
            catch
            {
                return false;
            }

        }

    }
}
