using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SphereSSLv2.Services.Config;
using SphereSSLv2.Models.ConfigModels;

namespace SphereSSLv2.Services.Security.Auth
{
    public class PasswordService
    {


        public static string HashPassword(string password)
        {
            try
            {
                string prehashedPass = Convert.ToBase64String(SHA512.HashData(Encoding.UTF8.GetBytes(password)));

                using var rng = RandomNumberGenerator.Create();
                byte[] salt = new byte[16];
                rng.GetBytes(salt);

                using var pbkdf2 = new Rfc2898DeriveBytes(prehashedPass, salt, 100000, HashAlgorithmName.SHA256);
                byte[] hash = pbkdf2.GetBytes(32);

                byte[] hashBytes = new byte[48];
                Array.Copy(salt, 0, hashBytes, 0, 16);
                Array.Copy(hash, 0, hashBytes, 16, 32);

                return Convert.ToBase64String(hashBytes);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static bool VerifyPassword(string password, string storedHash)
        {
            try
            {
                byte[] hashBytes = Convert.FromBase64String(storedHash);

                if (hashBytes.Length != 48)
                    return false;

                byte[] salt = new byte[16];
                Array.Copy(hashBytes, 0, salt, 0, 16);

                using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 100000, HashAlgorithmName.SHA256);
                byte[] hash = pbkdf2.GetBytes(32);

                byte[] storedPasswordHash = new byte[32];
                Array.Copy(hashBytes, 16, storedPasswordHash, 0, 32);

                return TimingSafeEquals(hash, storedPasswordHash);
            }
            catch
            {
                return false;
            }
        }

        public static string HashString(string password)
        {
            try
            {
                string hashedPass = Convert.ToBase64String(SHA512.HashData(Encoding.UTF8.GetBytes(password)));

                return hashedPass;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool TimingSafeEquals(byte[] a, byte[] b)
        {
            uint diff = (uint)a.Length ^ (uint)b.Length;
            for (int i = 0; i < a.Length && i < b.Length; i++)
                diff |= (uint)(a[i] ^ b[i]);
            return diff == 0;
        }

        private static async Task UpdateSavedPassword(string password)
        {
            try
            {
                string hashedPassword = HashPassword(password);
                if (hashedPassword == null)
                {
                    throw new InvalidOperationException("Failed to hash password.");
                }
                DeviceConfig config = new DeviceConfig
                {
                    UsePassword = ConfigureService.UseLogOn,
                    ServerURL = ConfigureService.ServerIP,
                    ServerPort = ConfigureService.ServerPort,
                    Username = ConfigureService.Username,
                    PasswordHash = hashedPassword
                };
                string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(ConfigureService.ConfigFilePath, json);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to update saved password.", ex);
            }
        }


    }
}
