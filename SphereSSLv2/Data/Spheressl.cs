using DnsClient;
using DnsClient.Protocol;
using SphereSSL2.Model;
using SphereSSL2.View;
using SphereSSLv2.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SphereSSLv2.Data
{
    public class Spheressl
    {

        internal static bool UseLogOn = true;
        internal static bool IsLogIn = false;
        internal static string ConfigFilePath = "app.config";
        internal static Process TrayAppProcess;
        internal static string TrayAppPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SphereSSL.exe");
        internal static string ServerIP { get; set; } = "127.0.0.1";
        internal static int ServerPort { get; set; } = 7171;
        public static double RefreshExpiringSoonRateInMinutes { get; } = 5;
        public static double ExpiringNoticePeriodInDays { get; } = 30;

        internal static string dbPath = "certificates.db";
        internal static string HashedPassword = string.Empty;
        internal static string Username = string.Empty;
        internal static string AutoLaunchBrowser = "true";
        internal static List<CertRecord> ExpiredCertRecords = new List<CertRecord>();
        internal static List<CertRecord> ExpiringSoonCertRecords = new List<CertRecord>();
        internal static List<CertRecord> CertRecords = new List<CertRecord>();
        internal static List<DNSProvider> DNSProviders = new List<DNSProvider>();

        //for testing
        internal static bool GenerateFakeTestCerts=false;



        internal static void OnProcessExit(object? sender, EventArgs e)
        {
            if (TrayAppProcess != null && !TrayAppProcess.HasExited)
            {
                try
                {
                    TrayAppProcess.Kill();
                    TrayAppProcess.Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to kill tray app: {ex.Message}");
                }
            }
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
                    UsePassword = UseLogOn,
                    ServerURL = ServerIP,
                    ServerPort = ServerPort,
                    Username = Username,
                    PasswordHash = hashedPassword
                };
                string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(ConfigFilePath, json);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to update saved password.", ex);
            }
        }

        private static async Task SaveConfigFile(DeviceConfig config)
        {
            try
            {
                string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(ConfigFilePath, json);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to update saved config.", ex);
            }
        }

        private static async Task UpdateConfigSettings(DeviceConfig config)
        {
            UseLogOn = config.UsePassword;
            ServerIP = config.ServerURL;
            ServerPort = config.ServerPort;
            Username = config.Username;
            HashedPassword = config.PasswordHash;
        }

        internal static async Task<DeviceConfig> LoadConfigFile()
        {
            try
            {
                string json = File.ReadAllText(ConfigFilePath);

                DeviceConfig config = JsonSerializer.Deserialize<DeviceConfig>(json);

                if (config == null)
                {
                    throw new InvalidOperationException("Failed to deserialize node config.");
                }


                return config;


            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to load config file.", ex);
            }
        }

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
                {
                    return false;
                }

                byte[] salt = new byte[16];
                Array.Copy(hashBytes, 0, salt, 0, 16);

                using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 100000, HashAlgorithmName.SHA256);
                byte[] hash = pbkdf2.GetBytes(32);

                bool isMatch = true;
                for (int i = 0; i < 32; i++)
                {
                    if (hashBytes[i + 16] != hash[i]) isMatch = false;
                }

                return isMatch;
            }
            catch (FormatException)
            {
                return false;
            }
            catch (ArgumentNullException)
            {
                return false;
            }
            catch (Exception)
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



        public static string CapitalizeFirstLetter(string input)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(input))
                    return input;

                return char.ToUpper(input[0]) + input.Substring(1);
            }
            catch (Exception)
            {

                return input;
            }
        }

        public static async Task<List<string>> GetNameServers(string domain)
        {
            var lookup = new LookupClient();
            var result = await lookup.QueryAsync(domain, QueryType.NS);

            return result.Answers
                .OfType<DnsClient.Protocol.NsRecord>()
                .Select(ns => ns.NSDName.Value)
                .ToList();
        }

        public static async Task<(string, string)> ExtractDnsProvider(string nsRecord)
        {
            if (string.IsNullOrWhiteSpace(nsRecord))
                return ("Unknown", "Unknown.com");

            var parts = nsRecord.ToLower().TrimEnd('.').Split('.');

            if (parts.Length >= 2)
                return (parts[^2], parts[^2] + "." + parts[^1]);

            return ("Unknown","Unknown.com");
        }

        public static async Task<(string, string)> GetNameServersProvider(string domain)
        {
            
            var results = await GetNameServers(domain);

            if (results == null || results.Count == 0)
            {
                Logger.Info($"NameServer Provider Not Located for domain {domain}");
                return ("Unknown", "Unknown.com");
            }
          
            return await ExtractDnsProvider(results[0]);
        }
    }



}
