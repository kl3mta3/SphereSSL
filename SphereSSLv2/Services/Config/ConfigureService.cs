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
        internal static string CAPrimeUrl ;
        internal static string CAStagingUrl ;
        internal static string dbPath = "certificates.db";
        internal static string HashedPassword = string.Empty;
        internal static string Username = string.Empty;
        internal static string AutoLaunchBrowser = "true";
        internal static List<CertRecord> ExpiredCertRecords = new List<CertRecord>();
        internal static List<CertRecord> ExpiringSoonCertRecords = new List<CertRecord>();
        internal static List<CertRecord> CertRecords = new List<CertRecord>();
        internal static List<DNSProvider> DNSProviders = new List<DNSProvider>();
        internal static bool IsSetup = false;
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
                    throw new InvalidOperationException($"Failed to kill TrayAppProcess: {ex.Message}");
                }
            }
        }

        internal static async Task SaveConfigFile(StoredConfig config)
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

        internal static async Task UpdateConfigFile(StoredConfig config)
        {
            try
            {

                StoredConfig oldConfig = await LoadConfigFile();

                if (!String.IsNullOrWhiteSpace(config.ServerURL))
                {
                    oldConfig.ServerURL = config.ServerURL;
                    ServerIP = config.ServerURL;
                }

                if (config.ServerPort != 0 && oldConfig.ServerPort != config.ServerPort)
                {
                    oldConfig.ServerPort = config.ServerPort;
                    ServerPort = config.ServerPort;
                }

                if (!String.IsNullOrWhiteSpace(config.AdminUsername) && oldConfig.AdminUsername != config.AdminUsername)
                {

                    oldConfig.AdminUsername = config.AdminUsername;
                    Username = config.AdminUsername;
                }

                if (!String.IsNullOrWhiteSpace(config.AdminPassword) && oldConfig.AdminPassword != config.AdminPassword)
                {
                    oldConfig.AdminPassword = config.AdminPassword;
                    HashedPassword = PasswordService.HashPassword(config.AdminPassword);
                }

                if (!String.IsNullOrWhiteSpace(config.DatabasePath) && oldConfig.DatabasePath != config.DatabasePath)
                {
                    oldConfig.DatabasePath = config.DatabasePath;
                    dbPath = config.DatabasePath;
                }

                if (oldConfig.UseLogOn != config.UseLogOn)
                {
                    oldConfig.UseLogOn = config.UseLogOn;
                    UseLogOn = config.UseLogOn ? true : false;
                }

                if (!string.IsNullOrWhiteSpace(config.CAPrimeUrl) && oldConfig.CAPrimeUrl != config.CAPrimeUrl)
                {
                    oldConfig.CAPrimeUrl = config.CAPrimeUrl;
                    CAPrimeUrl = config.CAPrimeUrl;
                }

                if (!string.IsNullOrWhiteSpace(config.CAStagingUrl) && oldConfig.CAStagingUrl != config.CAStagingUrl)
                {
                    oldConfig.CAStagingUrl = config.CAStagingUrl;
                    CAStagingUrl = config.CAStagingUrl;
                }

                string json = JsonSerializer.Serialize(oldConfig, new JsonSerializerOptions { WriteIndented = true });
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

        internal static async Task<StoredConfig> LoadConfigFile()
        {
            try
            {
                
                var storedConfig = new StoredConfig();
                for (int i = 0; i < 3; i++)
                {
                    string json = File.ReadAllText(ConfigFilePath);
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    storedConfig = JsonSerializer.Deserialize<StoredConfig>(json, options);
      
                    if (!string.IsNullOrWhiteSpace(json) && json.Trim() != "{}")
            
                        break;
                }
              
                if (storedConfig == null)
                {
                    throw new InvalidOperationException("Failed to deserialize node config.");
                }
                string passhash = PasswordService.HashPassword(storedConfig.AdminPassword);
            
              
                if (!storedConfig.UseLogOn )
                {
                    UseLogOn = false;
                  
                }
                else if (storedConfig.UseLogOn)
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
                ServerIP = storedConfig.ServerURL;
                dbPath = storedConfig.DatabasePath;
                CAPrimeUrl = storedConfig.CAPrimeUrl;
                CAStagingUrl = storedConfig.CAStagingUrl;


                return storedConfig;

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
