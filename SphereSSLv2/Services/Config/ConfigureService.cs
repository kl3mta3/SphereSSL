// Ignore Spelling: Spheressl

using DnsClient;
using DnsClient.Protocol;
using Microsoft.AspNetCore.SignalR;
using SphereSSL2.View;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CertRecord = SphereSSLv2.Models.CertModels.CertRecord;
using SphereSSLv2.Models.ConfigModels;
using SphereSSLv2.Models.DNSModels;
using SphereSSLv2.Services.Security.Auth;
using static Org.BouncyCastle.Math.EC.ECCurve;

namespace SphereSSLv2.Services.Config
{
    public class ConfigureService
    {

        internal static bool UseLogOn = false;
        internal static bool IsLogIn = false;
        internal static string ConfigFilePath = "app.config";
        internal static Process TrayAppProcess;
        internal static string TrayAppPath = Path.Combine(AppContext.BaseDirectory, "SphereSSL.exe");
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

        private readonly Logger _logger;

        public ConfigureService(Logger logger)
        {
            _logger = logger;
        }

        //for testing
        internal static bool GenerateFakeTestCerts = true;

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

        internal static async Task LoadConfigFile()
        {
            try
            {
                string json = File.ReadAllText(ConfigFilePath);

                var storedConfig = JsonSerializer.Deserialize<StoredConfig>(json);

                if (storedConfig == null)
                {
                    throw new InvalidOperationException("Failed to deserialize node config.");
                }
                string passhash = PasswordService.HashPassword(storedConfig.AdminPassword);

             
                if (storedConfig.UseLogOn == "false")
                {
                    UseLogOn = false; 
                }
                else if(storedConfig.UseLogOn == "true")
                {

                   UseLogOn = true;

                }
                else
                {
                    UseLogOn = false; 

                }

                Username = storedConfig.AdminUsername ?? string.Empty;
                HashedPassword = passhash;
                ServerPort = storedConfig.ServerPort > 0 ? storedConfig.ServerPort : 7171;
                ServerIP = string.IsNullOrWhiteSpace(storedConfig.ServerURL)
                ? "127.0.0.1"
                : storedConfig.ServerURL;



            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to load config file.", ex);
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
                .OfType<NsRecord>()
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

            return ("Unknown", "Unknown.com");
        }

        public async Task<(string, string)> GetNameServersProvider(string domain)
        {

            var results = await GetNameServers(domain);

            if (results == null || results.Count == 0)
            {
                await _logger.Info($"NameServer Provider Not Located for domain {domain}");
                return ("Unknown", "Unknown.com");
            }

            return await ExtractDnsProvider(results[0]);
        }

    }

}
