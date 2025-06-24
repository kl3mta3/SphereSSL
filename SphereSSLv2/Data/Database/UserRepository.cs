using Microsoft.Data.Sqlite;
using Microsoft.VisualBasic.ApplicationServices;
using User = SphereSSLv2.Models.UserModels.User;
using SphereSSLv2.Services.Config;
using SphereSSLv2.Models.UserModels;
using SphereSSLv2.Models.DNSModels;


namespace SphereSSLv2.Data.Database
{
    public class UserRepository
    {
        private readonly DnsProviderRepository _dnsProviderRepository;

        public UserRepository(DnsProviderRepository dnsProviderRepository)
        {
            _dnsProviderRepository = dnsProviderRepository;
        }

        public async Task<List<DNSProvider>> GetUserDnsProviders(string userId)
        {
            return await DnsProviderRepository.GetAllDNSProvidersByUserId(userId);
        }

        //User Management Repository

        public async Task InsertUserintoDatabaseAsync(User user)
        {
            if (user == null) return;

            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
        INSERT INTO Users (
            UserId, Username, PasswordHash, Name, Email, 
            CreationTime, LastUpdated, UUID, Notes
        )
        VALUES (
            @UserId, @Username, @PasswordHash, @Name, @Email, 
            @CreationTime, @LastUpdated, @UUID, @Notes
        );";

            command.Parameters.AddWithValue("@UserId", user.UserId);
            command.Parameters.AddWithValue("@Username", user.Username);
            command.Parameters.AddWithValue("@PasswordHash", user.PasswordHash);
            command.Parameters.AddWithValue("@Name", user.Name);
            command.Parameters.AddWithValue("@Email", user.Email);
            command.Parameters.AddWithValue("@CreationTime", user.CreationTime.ToString("o"));
            command.Parameters.AddWithValue("@LastUpdated", user.LastUpdated?.ToString("o") ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@UUID", user.UUID);
            command.Parameters.AddWithValue("@Notes", user.Notes ?? string.Empty);

            await command.ExecuteNonQueryAsync();
        }

        public async Task<User?> GetUserByUsernameAsync(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return null;

            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();

            command.CommandText = @"
            SELECT UserId, Username, PasswordHash, Name, Email, CreationTime, LastUpdated, UUID, Notes
                FROM Users
                WHERE Username = @username
            LIMIT 1;";

            command.Parameters.AddWithValue("@username", username);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new User
                {
                    UserId = reader.GetString(reader.GetOrdinal("UserId")),
                    Username = reader.GetString(reader.GetOrdinal("Username")),
                    PasswordHash = reader.GetString(reader.GetOrdinal("PasswordHash")),
                    Name = reader.GetString(reader.GetOrdinal("Name")),
                    Email = reader.GetString(reader.GetOrdinal("Email")),
                    CreationTime = DateTime.Parse(reader.GetString(reader.GetOrdinal("CreationTime"))),
                    LastUpdated = reader.IsDBNull(reader.GetOrdinal("LastUpdated"))
                        ? null
                        : DateTime.Parse(reader.GetString(reader.GetOrdinal("LastUpdated"))),
                    UUID = reader.GetString(reader.GetOrdinal("UUID")),
                    Notes = reader.IsDBNull(reader.GetOrdinal("Notes"))
                        ? null
                        : reader.GetString(reader.GetOrdinal("Notes"))
                };
            }

            return null;
        }

        public async Task<User?> GetUserByEmailAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return null;

            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
            SELECT UserId, Username, PasswordHash, Name, Email, CreationTime, LastUpdated, UUID, Notes
                FROM Users
                WHERE Email = @Email
            LIMIT 1;
            ";
            command.Parameters.AddWithValue("@Email", email);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new User
                {
                    UserId = reader.GetString(reader.GetOrdinal("UserId")),
                    Username = reader.GetString(reader.GetOrdinal("Username")),
                    PasswordHash = reader.GetString(reader.GetOrdinal("PasswordHash")),
                    Name = reader.GetString(reader.GetOrdinal("Name")),
                    Email = reader.GetString(reader.GetOrdinal("Email")),
                    CreationTime = DateTime.Parse(reader.GetString(reader.GetOrdinal("CreationTime"))),
                    LastUpdated = reader.IsDBNull(reader.GetOrdinal("LastUpdated"))
                        ? null
                        : DateTime.Parse(reader.GetString(reader.GetOrdinal("LastUpdated"))),
                    UUID = reader.GetString(reader.GetOrdinal("UUID")),
                    Notes = reader.IsDBNull(reader.GetOrdinal("Notes"))
                        ? null
                        : reader.GetString(reader.GetOrdinal("Notes"))
                };
            }

            return null;
        }

        public async Task<User?> GetUserByIdAsync(string userId)
        {

            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
        SELECT UserId, Username, PasswordHash, Name, Email, CreationTime, LastUpdated, UUID, Notes
        FROM Users
        WHERE UserId = @UserId
        LIMIT 1;
    ";
            command.Parameters.AddWithValue("@UserId", userId);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new User
                {
                    UserId = reader.GetString(reader.GetOrdinal("UserId")),
                    Username = reader.GetString(reader.GetOrdinal("Username")),
                    PasswordHash = reader.GetString(reader.GetOrdinal("PasswordHash")),
                    Name = reader.GetString(reader.GetOrdinal("Name")),
                    Email = reader.GetString(reader.GetOrdinal("Email")),
                    CreationTime = DateTime.Parse(reader.GetString(reader.GetOrdinal("CreationTime"))),
                    LastUpdated = reader.IsDBNull(reader.GetOrdinal("LastUpdated"))
                        ? null
                        : DateTime.Parse(reader.GetString(reader.GetOrdinal("LastUpdated"))),
                    UUID = reader.GetString(reader.GetOrdinal("UUID")),
                    Notes = reader.IsDBNull(reader.GetOrdinal("Notes"))
                        ? null
                        : reader.GetString(reader.GetOrdinal("Notes"))
                };
            }

            return null;


        }

        public async Task<User?> GetUserByUUIDAsync(string uuid)
        {

            if (string.IsNullOrWhiteSpace(uuid))
                return null;

            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
            SELECT UserId, Username, PasswordHash, Name, Email, CreationTime, LastUpdated, UUID, Notes
                FROM Users
                WHERE UUID = @UUID
            LIMIT 1;
             ";
            command.Parameters.AddWithValue("@UUID", uuid);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new User
                {
                    UserId = reader.GetString(reader.GetOrdinal("UserId")),
                    Username = reader.GetString(reader.GetOrdinal("Username")),
                    PasswordHash = reader.GetString(reader.GetOrdinal("PasswordHash")),
                    Name = reader.GetString(reader.GetOrdinal("Name")),
                    Email = reader.GetString(reader.GetOrdinal("Email")),
                    CreationTime = DateTime.Parse(reader.GetString(reader.GetOrdinal("CreationTime"))),
                    LastUpdated = reader.IsDBNull(reader.GetOrdinal("LastUpdated"))
                        ? null
                        : DateTime.Parse(reader.GetString(reader.GetOrdinal("LastUpdated"))),
                    UUID = reader.GetString(reader.GetOrdinal("UUID")),
                    Notes = reader.IsDBNull(reader.GetOrdinal("Notes"))
                        ? null
                        : reader.GetString(reader.GetOrdinal("Notes"))
                };
            }

            return null;

        }

        public async Task<List<User>> GetAllUsersAsync()
        {
            var users = new List<User>();

            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
            SELECT UserId, Username, PasswordHash, Name, Email, CreationTime, LastUpdated, UUID, Notes
            FROM Users
            ORDER BY Username;
            ";

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var user = new User
                {
                    UserId = reader.GetString(reader.GetOrdinal("UserId")),
                    Username = reader.GetString(reader.GetOrdinal("Username")),
                    PasswordHash = reader.GetString(reader.GetOrdinal("PasswordHash")),
                    Name = reader.GetString(reader.GetOrdinal("Name")),
                    Email = reader.GetString(reader.GetOrdinal("Email")),
                    CreationTime = DateTime.Parse(reader.GetString(reader.GetOrdinal("CreationTime"))),
                    LastUpdated = reader.IsDBNull(reader.GetOrdinal("LastUpdated"))
                        ? null
                        : DateTime.Parse(reader.GetString(reader.GetOrdinal("LastUpdated"))),
                    UUID = reader.GetString(reader.GetOrdinal("UUID")),
                    Notes = reader.IsDBNull(reader.GetOrdinal("Notes"))
                        ? null
                        : reader.GetString(reader.GetOrdinal("Notes"))
                };

                users.Add(user);
            }

            return users;


        }

        public async Task UpdateUserAsync(User user)
        {

            if (user == null) return;

            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
            UPDATE Users SET
                Username = @Username,
                PasswordHash = @PasswordHash,
                Name = @Name,
                Email = @Email,
                CreationTime = @CreationTime,
                LastUpdated = @LastUpdated,
                UUID = @UUID,
                Notes = @Notes
            WHERE UserId = @UserId;
             ";

            command.Parameters.AddWithValue("@Username", user.Username);
            command.Parameters.AddWithValue("@PasswordHash", user.PasswordHash);
            command.Parameters.AddWithValue("@Name", user.Name);
            command.Parameters.AddWithValue("@Email", user.Email);
            command.Parameters.AddWithValue("@CreationTime", user.CreationTime.ToString("o"));
            command.Parameters.AddWithValue("@LastUpdated", user.LastUpdated.HasValue
                ? user.LastUpdated.Value.ToString("o")
                : DBNull.Value);
            command.Parameters.AddWithValue("@UUID", user.UUID);
            command.Parameters.AddWithValue("@Notes", user.Notes ?? string.Empty);
            command.Parameters.AddWithValue("@UserId", user.UserId);

            await command.ExecuteNonQueryAsync();

        }

        public async Task DeleteUserAsync(int userId)
        {

            if (userId <= 0) return;

            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
            DELETE FROM Users
                WHERE Id = @Id;
            ";
            command.Parameters.AddWithValue("@Id", userId);

            await command.ExecuteNonQueryAsync();


        }

        public async Task<bool> IsUsernameAvailableAsync(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return false;

            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
            SELECT 1 FROM Users
                WHERE Username = @Username
                LIMIT 1;
            ";
            command.Parameters.AddWithValue("@Username", username);

            var result = await command.ExecuteScalarAsync();
            return result == null;

        }

        public async Task<bool> IsEmailAvailableAsync(string email)
        {

            if (string.IsNullOrWhiteSpace(email))
                return false;

            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
            SELECT 1 FROM Users
                WHERE Email = @Email
                LIMIT 1;
            ";
            command.Parameters.AddWithValue("@Email", email);

            var result = await command.ExecuteScalarAsync();
            return result == null;

        }

        public async Task<bool> IsUUIDAvailableAsync(string uuid)
        {
            if (string.IsNullOrWhiteSpace(uuid))
                return false;

            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
            SELECT 1 FROM Users
                WHERE UUID = @UUID
                LIMIT 1;
            ";
            command.Parameters.AddWithValue("@UUID", uuid);

            var result = await command.ExecuteScalarAsync();
            return result == null;

        }

        public async Task<User?> AuthenticateUserAsync(string username, string passwordHash)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(passwordHash))
                return null;

            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
            SELECT UserId, Username, PasswordHash, Name, Email, CreationTime, LastUpdated, UUID, Notes
                FROM Users
                WHERE Username = @Username AND PasswordHash = @PasswordHash
                LIMIT 1;
             ";
            command.Parameters.AddWithValue("@Username", username);
            command.Parameters.AddWithValue("@PasswordHash", passwordHash);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new User
                {
                    UserId = reader.GetString(reader.GetOrdinal("UserId")),
                    Username = reader.GetString(reader.GetOrdinal("Username")),
                    PasswordHash = reader.GetString(reader.GetOrdinal("PasswordHash")),
                    Name = reader.GetString(reader.GetOrdinal("Name")),
                    Email = reader.GetString(reader.GetOrdinal("Email")),
                    CreationTime = DateTime.Parse(reader.GetString(reader.GetOrdinal("CreationTime"))),
                    LastUpdated = reader.IsDBNull(reader.GetOrdinal("LastUpdated"))
                        ? null
                        : DateTime.Parse(reader.GetString(reader.GetOrdinal("LastUpdated"))),
                    UUID = reader.GetString(reader.GetOrdinal("UUID")),
                    Notes = reader.IsDBNull(reader.GetOrdinal("Notes"))
                        ? null
                        : reader.GetString(reader.GetOrdinal("Notes"))
                };
            }

            return null;
        }



        //UserStat Management

        public async Task InsertUserStatAsync(UserStat userStat)
        {
            if (userStat == null) return;
            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
            INSERT INTO UserStats (UserId, TotalCerts, CertsRenewed, CertsFailed, LastCertCreated)
                VALUES (@UserId, @TotalCerts, @CertsRenewed, @CertsFailed, @LastCertCreated);
            ";
            command.Parameters.AddWithValue("@UserId", userStat.UserId);
            command.Parameters.AddWithValue("@TotalCerts", userStat.TotalCerts);
            command.Parameters.AddWithValue("@CertsRenewed", userStat.CertsRenewed);
            command.Parameters.AddWithValue("@CertsFailed", userStat.CertsFailed);
            command.Parameters.AddWithValue("@LastCertCreated",
                userStat.LastCertCreated.HasValue ? userStat.LastCertCreated.Value.ToString("o") : (object)DBNull.Value);

            await command.ExecuteNonQueryAsync();
        }

        public async Task<UserStat?> GetUserStatByIdAsync(string userId)
        {


            if (string.IsNullOrWhiteSpace(userId)) return null;
            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
            SELECT UserId, TotalCerts, CertsRenewed, CertsFailed, LastCertCreated
                FROM UserStats WHERE UserId = @UserId LIMIT 1;
            ";
            command.Parameters.AddWithValue("@UserId", userId);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new UserStat
                {
                    UserId = reader.GetString(reader.GetOrdinal("UserId")),
                    TotalCerts = reader.GetInt32(reader.GetOrdinal("TotalCerts")),
                    CertsRenewed = reader.GetInt32(reader.GetOrdinal("CertsRenewed")),
                    CertsFailed = reader.GetInt32(reader.GetOrdinal("CertsFailed")),
                    LastCertCreated = reader.IsDBNull(reader.GetOrdinal("LastCertCreated"))
                        ? null
                        : DateTime.Parse(reader.GetString(reader.GetOrdinal("LastCertCreated")))
                };
            }
            return null;

        }

        public async Task<List<UserStat>> GetAllUserStatsAsync()
        {
            var stats = new List<UserStat>();
            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT UserId, TotalCerts, CertsRenewed, CertsFailed, LastCertCreated FROM UserStats;";

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                stats.Add(new UserStat
                {
                    UserId = reader.GetString(reader.GetOrdinal("UserId")),
                    TotalCerts = reader.GetInt32(reader.GetOrdinal("TotalCerts")),
                    CertsRenewed = reader.GetInt32(reader.GetOrdinal("CertsRenewed")),
                    CertsFailed = reader.GetInt32(reader.GetOrdinal("CertsFailed")),
                    LastCertCreated = reader.IsDBNull(reader.GetOrdinal("LastCertCreated"))
                        ? null
                        : DateTime.Parse(reader.GetString(reader.GetOrdinal("LastCertCreated")))
                });
            }
            return stats;
        }

        public async Task UpdateUserStatAsync(UserStat userStat)
        {
            if (userStat == null) return;
            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
            UPDATE UserStats SET
                TotalCerts = @TotalCerts,
                CertsRenewed = @CertsRenewed,
                CertsFailed = @CertsFailed,
                LastCertCreated = @LastCertCreated
            WHERE UserId = @UserId;
            ";
            command.Parameters.AddWithValue("@UserId", userStat.UserId);
            command.Parameters.AddWithValue("@TotalCerts", userStat.TotalCerts);
            command.Parameters.AddWithValue("@CertsRenewed", userStat.CertsRenewed);
            command.Parameters.AddWithValue("@CertsFailed", userStat.CertsFailed);
            command.Parameters.AddWithValue("@LastCertCreated",
                userStat.LastCertCreated.HasValue ? userStat.LastCertCreated.Value.ToString("o") : (object)DBNull.Value);

            await command.ExecuteNonQueryAsync();


        }

        public async Task DeleteUserStatAsync(string userId)
        {

            if (string.IsNullOrWhiteSpace(userId)) return;
            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM UserStats WHERE UserId = @UserId;";
            command.Parameters.AddWithValue("@UserId", userId);

            await command.ExecuteNonQueryAsync();
        }







        // UserRole Management
        public async Task InsertUserRoleAsync(UserRole userRole)
        {
            if (userRole == null) return;
            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
            INSERT INTO UserRoles (UserId, IsAdmin, IsEnabled, Role)
                VALUES (@UserId, @IsAdmin, @IsEnabled, @Role);
            ";
            command.Parameters.AddWithValue("@UserId", userRole.UserId);
            command.Parameters.AddWithValue("@IsAdmin", userRole.IsAdmin ? 1 : 0);
            command.Parameters.AddWithValue("@IsEnabled", userRole.IsEnabled ? 1 : 0);
            command.Parameters.AddWithValue("@Role", userRole.Role);

            await command.ExecuteNonQueryAsync();
        }

        public async Task<UserRole?> GetUserRoleByIdAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId)) return null;
            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
            SELECT UserId, IsAdmin, IsEnabled, Role
                FROM UserRoles WHERE UserId = @UserId LIMIT 1;
            ";
            command.Parameters.AddWithValue("@UserId", userId);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new UserRole
                {
                    UserId = reader.GetString(reader.GetOrdinal("UserId")),
                    IsAdmin = reader.GetBoolean(reader.GetOrdinal("IsAdmin")),
                    IsEnabled = reader.GetBoolean(reader.GetOrdinal("IsEnabled")),
                    Role = reader.GetString(reader.GetOrdinal("Role"))
                };
            }
            return null;
        }

        public async Task<List<UserRole>> GetAllUserRolesAsync()
        {
            var roles = new List<UserRole>();
            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT UserId, IsAdmin, IsEnabled, Role FROM UserRoles;";

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                roles.Add(new UserRole
                {
                    UserId = reader.GetString(reader.GetOrdinal("UserId")),
                    IsAdmin = reader.GetBoolean(reader.GetOrdinal("IsAdmin")),
                    IsEnabled = reader.GetBoolean(reader.GetOrdinal("IsEnabled")),
                    Role = reader.GetString(reader.GetOrdinal("Role"))
                });
            }
            return roles;
        }

        public async Task UpdateUserRoleAsync(UserRole userRole)
        {
            if (userRole == null) return;
            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
            UPDATE UserRoles SET
                IsAdmin = @IsAdmin,
                IsEnabled = @IsEnabled,
                Role = @Role
            WHERE UserId = @UserId;
            ";
            command.Parameters.AddWithValue("@UserId", userRole.UserId);
            command.Parameters.AddWithValue("@IsAdmin", userRole.IsAdmin ? 1 : 0);
            command.Parameters.AddWithValue("@IsEnabled", userRole.IsEnabled ? 1 : 0);
            command.Parameters.AddWithValue("@Role", userRole.Role);

            await command.ExecuteNonQueryAsync();
        }

        public async Task DeleteUserRoleAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId)) return;
            using var connection = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM UserRoles WHERE UserId = @UserId;";
            command.Parameters.AddWithValue("@UserId", userId);

            await command.ExecuteNonQueryAsync();
        }





    }
}