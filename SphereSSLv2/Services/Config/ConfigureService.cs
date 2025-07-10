// Ignore Spelling: Spheressl

using DnsClient;
using DnsClient.Protocol;
using System.Diagnostics;
using System.Text.Json;
using CertRecord = SphereSSLv2.Models.CertModels.CertRecord;
using SphereSSLv2.Models.ConfigModels;
using SphereSSLv2.Models.DNSModels;
using SphereSSLv2.Services.AcmeServices;
using Nager.PublicSuffix.RuleProviders;
using Nager.PublicSuffix;
using SphereSSLv2.Data.Repositories;
using SphereSSLv2.Models.CertModels;
using SphereSSLv2.Models.UserModels;
using SphereSSLv2.Services.Security.Auth;

namespace SphereSSLv2.Services.Config
{
    public class ConfigureService
    {

        internal static bool UseLogOn = false;
        internal static bool IsLogIn = false;
        internal static string ConfigFilePath = "app.config";
        // Tray app removed for Docker compatibility
        // internal static Process TrayAppProcess;
        // internal static string TrayAppPath = Path.Combine(AppContext.BaseDirectory, "SphereSSL.exe");
        internal static string ServerIP { get; set; } = "0.0.0.0";
        internal static int ServerPort { get; set; } = 7171;
        public static double RefreshExpiringSoonRateInHours { get; } = 24;
        public static double ExpiringRefreshPeriodInDays { get; } = 30;
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
        public static Dictionary<string, AcmeService> AcmeServiceCache = new Dictionary<string, AcmeService>();
        public static Dictionary<string, CertRecord> CertRecordCache = new Dictionary<string, CertRecord>();
        public ConfigureService(Logger logger)
        {
            _logger = logger;
        }
        private static DnsProviderRepository _dnsProviderRepository = new();
        private static readonly UserRepository _userRepository = new(_dnsProviderRepository);

        //for testing
        internal static bool GenerateFakeTestCerts = false;

        internal static void OnProcessExit(object? sender, EventArgs e)
        {
            // Tray app cleanup removed for Docker compatibility
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
            if (domain.StartsWith("*."))
            {
                domain = domain.Substring(2);
            }

            var ruleProvider = new LocalFileRuleProvider("public_suffix_list.dat");
            await ruleProvider.BuildAsync();
            var domainParser = new DomainParser(ruleProvider);
            var domainInfo = domainParser.Parse(domain);
            string strippedDomain = domainInfo.RegistrableDomain;
            var lookup = new LookupClient();
            var result = await lookup.QueryAsync(strippedDomain, QueryType.NS);

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




            public static async Task SeedCertRecords()
            {
                var now = DateTime.UtcNow;

                List<string> SupportedAutoProviders = Enum.GetValues(typeof(DNSProvider.ProviderType))
               .Cast<DNSProvider.ProviderType>()
               .Select(p => p.ToString())
               .ToList();

               
                    var fakeUserId = Guid.NewGuid().ToString("N");
                    var fakeUser = new User
                    {
                        UserId = fakeUserId,
                        Username = $"Seed",
                        PasswordHash = PasswordService.HashPassword("testpass"),
                        Name = $"Seeduser",
                        Email = $"Seeduser@email.com",
                        CreationTime = DateTime.UtcNow,
                        LastUpdated = DateTime.UtcNow,
                        UUID = Guid.NewGuid().ToString(),
                        Notes = $"Seed User"

                    };

                    var fakeRole = new UserRole
                    {
                        UserId = fakeUserId,
                        Role = "Viewer",
                        IsAdmin = false,
                        IsEnabled = false,

                    };

                    var fakeStat = new UserStat
                    {
                        UserId = fakeUserId,
                        TotalCerts = 0,
                        CertsRenewed = 0,
                        CertCreationsFailed = 0,
                        CertRenewalsFailed = 0,
                        LastCertCreated = null
                    };

                    await _userRepository.InsertUserintoDatabaseAsync(fakeUser);
                    await _userRepository.InsertUserRoleAsync(fakeRole);
                    await _userRepository.InsertUserStatAsync(fakeStat);

                    string fakeProvider = SupportedAutoProviders[Random.Shared.Next(SupportedAutoProviders.Count)];
                    DateTime creationDate = now.AddDays(-Random.Shared.Next(1, 100));
                    var cert = new CertRecord
                    {
                        UserId = fakeUserId,
                        OrderId = "12345",
                        Email = $"SeedUser@example.com",
                        SavePath = $"/fake/path/cert.pem",
                        UseSeparateFiles = 2 == 0,
                        SaveForRenewal = true,
                        autoRenew = 2 == 0,
                        FailedRenewals = Random.Shared.Next(0, 3),
                        SuccessfulRenewals = Random.Shared.Next(0, 10),
                        Signer = "MockSigner",
                        AccountID = $"acct-",
                        OrderUrl = $"https://acme.fake/",
                        ChallengeType = "dns-01",
                        Thumbprint = $"thumb",
                        Challenges = new List<AcmeChallenge>(),
                        CreationDate = creationDate,
                        ExpiryDate = creationDate.AddDays(90)
                    };

                    cert.Challenges = new List<AcmeChallenge>
                    {
                        new AcmeChallenge
                        {
                            ChallengeId = Guid.NewGuid().ToString("N"),
                            OrderId = cert.OrderId,
                            UserId = cert.UserId,
                            AuthorizationUrl = $"https://acme.fake/authorize/",
                            Domain =  $"test.example.com",
                            DnsChallengeToken =  $"token-",
                            Status = "Valid",
                            ZoneId = $"zone",
                            ProviderId=ConfigureService.CapitalizeFirstLetter(fakeProvider)
                        }
                    };

                    await CertRepository.InsertCertRecord(cert);
             }
     }
 }


