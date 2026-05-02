using Microsoft.Data.Sqlite;
using SphereSSLv2.Models.ConnectionModels;
using SphereSSLv2.Services.Config;

namespace SphereSSLv2.Data.Repositories
{
    public class ConnectionRepository
    {
        public async Task<List<UserConnection>> GetConnectionsByUserIdAsync(string userId)
        {
            var list = new List<UserConnection>();
            using var conn = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
            await conn.OpenAsync();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM UserConnections WHERE UserId = @UserId ORDER BY CreatedAt";
            cmd.Parameters.AddWithValue("@UserId", userId);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                list.Add(MapRow(reader));
            return list;
        }

        public async Task<UserConnection?> GetConnectionByIdAsync(string connectionId)
        {
            using var conn = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
            await conn.OpenAsync();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM UserConnections WHERE ConnectionId = @ConnectionId";
            cmd.Parameters.AddWithValue("@ConnectionId", connectionId);
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
                return MapRow(reader);
            return null;
        }

        public async Task<List<UserConnection>> GetAllConnectionsAsync()
        {
            var list = new List<UserConnection>();
            using var conn = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
            await conn.OpenAsync();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM UserConnections ORDER BY UserId, CreatedAt";
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                list.Add(MapRow(reader));
            return list;
        }

        public async Task<bool> InsertConnectionAsync(UserConnection connection)
        {
            try
            {
                using var conn = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
                await conn.OpenAsync();
                var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO UserConnections
                        (ConnectionId, UserId, ConnectionName, ConnectionType, IsEnabled, Settings,
                         OnPreRenew, OnPreExpiry, OnRenewSuccess, OnRenewFail, CreatedAt)
                    VALUES
                        (@ConnectionId, @UserId, @ConnectionName, @ConnectionType, @IsEnabled, @Settings,
                         @OnPreRenew, @OnPreExpiry, @OnRenewSuccess, @OnRenewFail, @CreatedAt)";
                cmd.Parameters.AddWithValue("@ConnectionId", connection.ConnectionId);
                cmd.Parameters.AddWithValue("@UserId", connection.UserId);
                cmd.Parameters.AddWithValue("@ConnectionName", connection.ConnectionName);
                cmd.Parameters.AddWithValue("@ConnectionType", connection.ConnectionType);
                cmd.Parameters.AddWithValue("@IsEnabled", connection.IsEnabled ? 1 : 0);
                cmd.Parameters.AddWithValue("@Settings", connection.Settings);
                cmd.Parameters.AddWithValue("@OnPreRenew", connection.OnPreRenew ? 1 : 0);
                cmd.Parameters.AddWithValue("@OnPreExpiry", connection.OnPreExpiry ? 1 : 0);
                cmd.Parameters.AddWithValue("@OnRenewSuccess", connection.OnRenewSuccess ? 1 : 0);
                cmd.Parameters.AddWithValue("@OnRenewFail", connection.OnRenewFail ? 1 : 0);
                cmd.Parameters.AddWithValue("@CreatedAt", connection.CreatedAt.ToString("o"));
                await cmd.ExecuteNonQueryAsync();
                return true;
            }
            catch { return false; }
        }

        public async Task<bool> UpdateConnectionAsync(UserConnection connection)
        {
            try
            {
                using var conn = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
                await conn.OpenAsync();
                var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    UPDATE UserConnections SET
                        ConnectionName  = @ConnectionName,
                        ConnectionType  = @ConnectionType,
                        IsEnabled       = @IsEnabled,
                        Settings        = @Settings,
                        OnPreRenew      = @OnPreRenew,
                        OnPreExpiry     = @OnPreExpiry,
                        OnRenewSuccess  = @OnRenewSuccess,
                        OnRenewFail     = @OnRenewFail
                    WHERE ConnectionId = @ConnectionId AND UserId = @UserId";
                cmd.Parameters.AddWithValue("@ConnectionId", connection.ConnectionId);
                cmd.Parameters.AddWithValue("@UserId", connection.UserId);
                cmd.Parameters.AddWithValue("@ConnectionName", connection.ConnectionName);
                cmd.Parameters.AddWithValue("@ConnectionType", connection.ConnectionType);
                cmd.Parameters.AddWithValue("@IsEnabled", connection.IsEnabled ? 1 : 0);
                cmd.Parameters.AddWithValue("@Settings", connection.Settings);
                cmd.Parameters.AddWithValue("@OnPreRenew", connection.OnPreRenew ? 1 : 0);
                cmd.Parameters.AddWithValue("@OnPreExpiry", connection.OnPreExpiry ? 1 : 0);
                cmd.Parameters.AddWithValue("@OnRenewSuccess", connection.OnRenewSuccess ? 1 : 0);
                cmd.Parameters.AddWithValue("@OnRenewFail", connection.OnRenewFail ? 1 : 0);
                await cmd.ExecuteNonQueryAsync();
                return true;
            }
            catch { return false; }
        }

        public async Task<bool> DeleteConnectionAsync(string connectionId, string userId)
        {
            try
            {
                using var conn = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
                await conn.OpenAsync();
                var cmd = conn.CreateCommand();
                cmd.CommandText = "DELETE FROM UserConnections WHERE ConnectionId = @ConnectionId AND UserId = @UserId";
                cmd.Parameters.AddWithValue("@ConnectionId", connectionId);
                cmd.Parameters.AddWithValue("@UserId", userId);
                await cmd.ExecuteNonQueryAsync();
                return true;
            }
            catch { return false; }
        }

        private static UserConnection MapRow(SqliteDataReader r) => new()
        {
            ConnectionId   = r["ConnectionId"]?.ToString()  ?? "",
            UserId         = r["UserId"]?.ToString()         ?? "",
            ConnectionName = r["ConnectionName"]?.ToString() ?? "",
            ConnectionType = r["ConnectionType"]?.ToString() ?? "",
            IsEnabled      = Convert.ToInt32(r["IsEnabled"])      == 1,
            Settings       = r["Settings"]?.ToString()       ?? "{}",
            OnPreRenew     = Convert.ToInt32(r["OnPreRenew"])     == 1,
            OnPreExpiry    = Convert.ToInt32(r["OnPreExpiry"])    == 1,
            OnRenewSuccess = Convert.ToInt32(r["OnRenewSuccess"]) == 1,
            OnRenewFail    = Convert.ToInt32(r["OnRenewFail"])    == 1,
            CreatedAt      = DateTime.TryParse(r["CreatedAt"]?.ToString(), out var dt) ? dt : DateTime.UtcNow
        };
    }
}
