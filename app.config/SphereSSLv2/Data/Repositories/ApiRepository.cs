using Microsoft.Data.Sqlite;
using SphereSSLv2.Models.UserModels;
using SphereSSLv2.Services.Config;

namespace SphereSSLv2.Data.Repositories
{
    public class ApiRepository
    {
        private readonly UserRepository userRepository;

        //API Key Management

        public async Task InsertApiKeyAsync(ApiKey apiKey)
        {
            if (apiKey == null) return;
            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
            INSERT INTO ApiKeys (UserId, ApiKey, Created, LastUsed, IsRevoked)
                VALUES (@UserId, @ApiKey, @Created, @LastUsed, @IsRevoked);
            ";
            command.Parameters.AddWithValue("@UserId", apiKey.UserId);
            command.Parameters.AddWithValue("@ApiKey", apiKey.APIKey);
            command.Parameters.AddWithValue("@Created", apiKey.Created.ToString("o"));
            command.Parameters.AddWithValue("@LastUsed", apiKey.LastUsed.HasValue ? apiKey.LastUsed.Value.ToString("o") : DBNull.Value);
            command.Parameters.AddWithValue("@IsRevoked", apiKey.IsRevoked ? 1 : 0);

            await command.ExecuteNonQueryAsync();
        }

        public async Task<ApiKey?> GetApiKeyByIdAsync(int apiKeyId)
        {
            if (apiKeyId <= 0) return null;
            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
            SELECT UserId, ApiKey, Created, LastUsed, IsRevoked
                FROM ApiKeys
            WHERE Id = @Id LIMIT 1;
            ";
            command.Parameters.AddWithValue("@Id", apiKeyId);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new ApiKey
                {
                    UserId = reader.GetString(reader.GetOrdinal("UserId")),
                    APIKey = reader.GetString(reader.GetOrdinal("ApiKey")),
                    Created = DateTime.Parse(reader.GetString(reader.GetOrdinal("Created"))),
                    LastUsed = reader.IsDBNull(reader.GetOrdinal("LastUsed")) ? null : DateTime.Parse(reader.GetString(reader.GetOrdinal("LastUsed"))),
                    IsRevoked = reader.GetBoolean(reader.GetOrdinal("IsRevoked"))
                };
            }
            return null;
        }

        public async Task<ApiKey?> GetApiKeyByUserIdAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId)) return null;
            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
            SELECT UserId, ApiKey, Created, LastUsed, IsRevoked
                FROM ApiKeys
            WHERE UserId = @UserId LIMIT 1;
            ";
            command.Parameters.AddWithValue("@UserId", userId);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new ApiKey
                {
                    UserId = reader.GetString(reader.GetOrdinal("UserId")),
                    APIKey = reader.GetString(reader.GetOrdinal("ApiKey")),
                    Created = DateTime.Parse(reader.GetString(reader.GetOrdinal("Created"))),
                    LastUsed = reader.IsDBNull(reader.GetOrdinal("LastUsed")) ? null : DateTime.Parse(reader.GetString(reader.GetOrdinal("LastUsed"))),
                    IsRevoked = reader.GetBoolean(reader.GetOrdinal("IsRevoked"))
                };
            }
            return null;
        }

        public async Task<List<ApiKey>> GetAllApiKeysAsync()
        {
            var keys = new List<ApiKey>();
            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
            SELECT UserId, ApiKey, Created, LastUsed, IsRevoked
                FROM ApiKeys;
            ";

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                keys.Add(new ApiKey
                {
                    UserId = reader.GetString(reader.GetOrdinal("UserId")),
                    APIKey = reader.GetString(reader.GetOrdinal("ApiKey")),
                    Created = DateTime.Parse(reader.GetString(reader.GetOrdinal("Created"))),
                    LastUsed = reader.IsDBNull(reader.GetOrdinal("LastUsed")) ? null : DateTime.Parse(reader.GetString(reader.GetOrdinal("LastUsed"))),
                    IsRevoked = reader.GetBoolean(reader.GetOrdinal("IsRevoked"))
                });
            }
            return keys;
        }

        public async Task UpdateApiKeyAsync(ApiKey apiKey)
        {
            if (apiKey == null) return;
            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
            UPDATE ApiKeys SET
                UserId = @UserId,
                ApiKey = @ApiKey,
                Created = @Created,
                LastUsed = @LastUsed,
                IsRevoked = @IsRevoked
            WHERE ApiKey = @ApiKey;
             ";
            command.Parameters.AddWithValue("@UserId", apiKey.UserId);
            command.Parameters.AddWithValue("@ApiKey", apiKey.APIKey);
            command.Parameters.AddWithValue("@Created", apiKey.Created.ToString("o"));
            command.Parameters.AddWithValue("@LastUsed", apiKey.LastUsed.HasValue ? apiKey.LastUsed.Value.ToString("o") : DBNull.Value);
            command.Parameters.AddWithValue("@IsRevoked", apiKey.IsRevoked ? 1 : 0);

            await command.ExecuteNonQueryAsync();
        }

        public async Task DeleteApiKeyAsync(int apiKeyId)
        {
            if (apiKeyId <= 0) return;
            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM ApiKeys WHERE Id = @Id;";
            command.Parameters.AddWithValue("@Id", apiKeyId);

            await command.ExecuteNonQueryAsync();
        }

        public async Task<bool> IsApiKeyAvailableAsync(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey)) return false;
            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT 1 FROM ApiKeys WHERE ApiKey = @ApiKey LIMIT 1;";
            command.Parameters.AddWithValue("@ApiKey", apiKey);

            var result = await command.ExecuteScalarAsync();
            return result == null;
        }

        public async Task<bool> IsApiKeyValidAsync(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return false;
            }
            var key = await GetApiKeyByIdAsync(int.Parse(apiKey));
            return key != null && key.IsRevoked;
        }

        public async Task<User?> GetUserByApiKeyAsync(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey)) return null;

            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
            await connection.OpenAsync();

            // First, get the userId from ApiKeys
            var command = connection.CreateCommand();
            command.CommandText = @"
            SELECT UserId FROM ApiKeys WHERE ApiKey = @ApiKey AND IsRevoked = 0 LIMIT 1;
             ";
            command.Parameters.AddWithValue("@ApiKey", apiKey);

            string? userId = null;
            using (var reader = await command.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                    userId = reader.GetString(reader.GetOrdinal("UserId"));
            }

            if (string.IsNullOrWhiteSpace(userId)) return null;

            return await userRepository.GetUserByIdAsync(userId);
        }

    }
}
