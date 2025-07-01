using ACMESharp.Crypto.JOSE.Impl;
using ACMESharp.Protocol;
using DnsClient.Protocol;
using Org.BouncyCastle.Asn1.Ocsp;
using SphereSSLv2.Data.Database;
using SphereSSLv2.Data.Repositories;
using SphereSSLv2.Models.CertModels;
using SphereSSLv2.Models.DNSModels;
using SphereSSLv2.Models.UserModels;
using SphereSSLv2.Services.AcmeServices;
using SphereSSLv2.Services.Config;
using System.Diagnostics;
using System.Security.Policy;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;
using CertRecord = SphereSSLv2.Models.CertModels.CertRecord;

namespace SphereSSLv2.Services.CertServices
{
    public class CertRecordServiceManager
    {
        internal DatabaseManager dbRecord;
        private readonly Logger _logger;
        internal readonly UserRepository _userRepository;
        internal readonly DnsProviderRepository _dnsProviderRepository;

        //public async Task LoadCertRecordServiceBat(string orderId)
        //{
        //    string batContent = dbRecord.GetRestartScriptById(orderId); // Get from DB
        //    string filePath = Path.Combine(AppContext.BaseDirectory, "Temp", "restart_script.bat");

        //    Directory.CreateDirectory(Path.GetDirectoryName(filePath)!); // Ensure Temp exists
        //    await File.WriteAllTextAsync(filePath, batContent); // Async write
        //}

        public async Task ExecuteCertRecordServiceBat(string batFilePath)
        {
            if (string.IsNullOrWhiteSpace(batFilePath))
                throw new ArgumentException("The path to the .bat file cannot be null or empty.", nameof(batFilePath));

            var processStartInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{batFilePath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = processStartInfo };
            process.Start();

            string output = await process.StandardOutput.ReadToEndAsync();
            string error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (!string.IsNullOrWhiteSpace(error))
                throw new Exception($"Error executing .bat file:\n{error}");


        }


        public async Task RenewCertRecordWithAutoDNSById(string orderId)
        {

            CertRecord order = await CertRepository.GetCertRecordByOrderId(orderId);

            if (order == null)
            {
                throw new Exception($"No certificate record found for order ID: {orderId}");
            }

            if (order.autoRenew)
            {
                ESJwsTool signer = AcmeService.LoadOrCreateSigner(new AcmeService(_logger));
                User user = await _userRepository.GetUserByIdAsync(order.UserId);
                if (user == null)
                {
                    await _logger.Error($"[{order.UserId}]: User not found for order ID: {orderId}");
                    return;
                }

                string username = user.Username;

                var url = order.OrderUrl;
                var uri = new Uri(url);
                var baseUrl = $"{uri.Scheme}://{uri.Host}/";
                var http = new HttpClient
                {
                    BaseAddress = new Uri(baseUrl),
                };

                var ACME = new AcmeService(_logger)
                {
                    _logger = _logger,
                    _signer = signer,
                    _client = new AcmeProtocolClient(http, null, null, signer),


                };
                ConfigureService.AcmeServiceCache.Add(order.OrderId, ACME);

                List<string> domains = order.Challenges.Select(c => c.Domain).ToList();
                var challenges = await ACME.CreateUserAccountForCert(order.Email, domains);

                if (challenges == null)
                {

                    await _logger.Error($"[{username}]: Returned domain is null or empty after CreateUserAccountForCert!");

                    return;
                }
                var updatedChallenges = new List<AcmeChallenge>();
                foreach (var challenge in challenges)
                {
                    AcmeChallenge currentChallange = order.Challenges.FirstOrDefault(c => c.Domain == challenge.Domain);
                    if (currentChallange == null)
                    {

                        await _logger.Error($"[{username}]: currentChallange domain:{challenge.Domain} is null or empty!");

                        return;
                    }
                    AcmeChallenge orderChallenge = new AcmeChallenge
                    {
                        ChallengeId = currentChallange.ChallengeId,
                        OrderId = order.OrderId,
                        UserId = order.UserId,
                        Domain = challenge.Domain,
                        DnsChallengeToken = challenge.DnsChallengeToken,
                        Status = "Processing",
                        ProviderId = currentChallange.ProviderId,
                        AuthorizationUrl = challenge.AuthorizationUrl,
                        ZoneId = currentChallange.ZoneId

                    };
                    updatedChallenges.Add(orderChallenge);

                }
                order.Challenges = updatedChallenges;
                foreach (var challenge in order.Challenges)
                {
                    if (string.IsNullOrWhiteSpace(challenge.Domain))
                    {
                        await _logger.Error($"[{username}]: Domain is null or empty for auto-adding DNS record.");
                        return;
                    }

                    List<DNSProvider> DNSProviders = await _dnsProviderRepository.GetAllDNSProviders();
                    DNSProvider _provider = DNSProviders.FirstOrDefault(p => p.ProviderId.Equals(challenge.ProviderId, StringComparison.OrdinalIgnoreCase));

                    if (_provider == null)
                    {
                        await _logger.Error($"[{username}]: No DNS provider found for {challenge.Domain} (ProviderId: {challenge.ProviderId})");
                        return;
                    }

                    await _logger.Info($"[{username}]: Auto-adding DNS record using provider: {_provider.ProviderName}");
                    var zoneID = await DNSProvider.TryAutoAddDNS(_logger, _provider, challenge.Domain, challenge.DnsChallengeToken, username);

                    if (String.IsNullOrWhiteSpace(zoneID))
                    {
                        await _logger.Info($"[{username}]: Failed to auto-add DNS record for provider: {_provider}");

                    }
                }
                const int maxAttempts = 5;
                int attempt = 0;

                while (attempt < maxAttempts)
                {
                    await _logger.Info($"[{username}]: Attempting DNS verification (try {attempt + 1} of {maxAttempts})...");

                    List<(AcmeChallenge challange, bool verified)> challengesResult;
                    try
                    {
                        challengesResult = await ACME.CheckTXTRecordMultipleDNS(order.Challenges, username);

                    }
                    catch (Exception ex)
                    {
                        await _logger.Error($"[{username}]: DNS verification failed: {ex.Message}");
                        await _logger.Debug($"[{username}]: DNS verification failed: {ex.Message}");
                        attempt++;
                        if (attempt < maxAttempts)
                        {
                            await _logger.Info($"[{username}]: Retrying in 15 seconds... (attempt {attempt + 1} of {maxAttempts})");
                            await Task.Delay(15000);
                        }
                        continue;
                    }

                    bool allVerified = true;
                    List<AcmeChallenge> failedChallenges = new();
                    foreach (var result in challengesResult)
                    {
                        if (!result.verified)
                        {
                            allVerified = false;

                            failedChallenges.Add(result.challange);
                        }
                    }

                    if (allVerified)
                    {
                        await _logger.Update($"[{username}]: DNS verification successful! Starting certificate generation...");
                        foreach (var _challenge in order.Challenges)
                        {
                            _challenge.Status = "Valid";
                        }

                        await ACME.ProcessCertificateGeneration(order.UseSeparateFiles, order.SavePath, order.Challenges, username);


                        if (order.SaveForRenewal)
                        {



                            await _logger.Update($"[{username}]: Saving order for renewal!");

                            await CertRepository.UpdateCertRecord(order);

                            UserStat stats = await _userRepository.GetUserStatByIdAsync(order.UserId);

                            if (stats == null)
                            {
                                stats = new UserStat
                                {
                                    UserId = order.UserId,
                                    TotalCerts = 1,
                                    CertsRenewed = 1,
                                    CertCreationsFailed = 0,
                                    LastCertCreated = DateTime.UtcNow
                                };
                            }
                            else
                            {
                                stats.CertsRenewed++;
                                stats.LastCertCreated = DateTime.UtcNow;
                            }

                        }
                        else
                        {
                            foreach (var __challenge in failedChallenges)
                            {
                                __challenge.Status = "Invalid";
                            }

                        }


                        if (!order.UseSeparateFiles)
                        {
                            await _logger.Update($"[{username}]: Certificate stored successfully!");
                        }
                        else
                        {
                            await _logger.Update($"[{username}]: Certificates stored successfully!");
                        }

                        return;
                    }
                    else
                    {
                        foreach (var failed in failedChallenges)
                        {
                            await _logger.Debug($"[{username}]: DNS verification failed for domain: {failed.Domain}");
                            await _logger.Debug($"[{username}]: Expected TXT record at: _acme-challenge.{failed.Domain}");
                            await _logger.Debug($"[{username}]: Expected value: {failed.DnsChallengeToken}");

                        }
                    }

                    attempt++;
                    if (attempt < maxAttempts)
                    {

                        await Task.Delay(15000);
                    }
                }

                await _logger.Error($"[{username}]: All {maxAttempts} attempts failed.");

            }
            else
            {
                throw new Exception($"Certificate with order ID {orderId} is not eligible for renewal.");
            }

        }


        public async Task<byte[]> CreateLocalPFXCert(string domain, string savePath, string password = "", int validDays = 365)
        {
            var pfx = CertUtilityService.CreateSelfSignedCert(
               subjectName: $"CN={domain}.local",
               outputPath: @$"{savePath}",
               password: password,
               validDays: validDays
           );

            return pfx;

        }

        public async Task<bool> StartManualRenewCertRecordById(string orderId)
        {
            CertRecord order = await CertRepository.GetCertRecordByOrderId(orderId);

            if (order == null)
            {
                throw new Exception($"No certificate record found for order ID: {orderId}");
            }


            ESJwsTool signer = AcmeService.LoadOrCreateSigner(new AcmeService(_logger));
            User user = await _userRepository.GetUserByIdAsync(order.UserId);
            if (user == null)
            {
                await _logger.Error($"[{order.UserId}]: User not found for order ID: {orderId}");
                return false;
            }

            string username = user.Username;

            var url = order.OrderUrl;
            var uri = new Uri(url);
            var baseUrl = $"{uri.Scheme}://{uri.Host}/";
            var http = new HttpClient
            {
                BaseAddress = new Uri(baseUrl),
            };

            var ACME = new AcmeService(_logger)
            {
                _logger = _logger,
                _signer = signer,
                _client = new AcmeProtocolClient(http, null, null, signer),


            };

            ConfigureService.AcmeServiceCache.Add(order.OrderId, ACME);

            List<string> domains = order.Challenges.Select(c => c.Domain).ToList();
            var challenges = await ACME.CreateUserAccountForCert(order.Email, domains);

            if (challenges == null)
            {

                await _logger.Error($"[{username}]: Returned domain is null or empty after CreateUserAccountForCert!");

                return false;
            }

            var updatedChallenges = new List<AcmeChallenge>();

            foreach (var challenge in challenges)
            {
                AcmeChallenge currentChallange = order.Challenges.FirstOrDefault(c => c.Domain == challenge.Domain);

                if (currentChallange == null)
                {

                    await _logger.Error($"[{username}]: currentChallange domain:{challenge.Domain} is null or empty!");

                    return false;
                }

                AcmeChallenge orderChallenge = new AcmeChallenge
                {
                    ChallengeId = currentChallange.ChallengeId,
                    OrderId = order.OrderId,
                    UserId = order.UserId,
                    Domain = challenge.Domain,
                    DnsChallengeToken = challenge.DnsChallengeToken,
                    Status = "Processing",
                    ProviderId = currentChallange.ProviderId,
                    AuthorizationUrl = challenge.AuthorizationUrl,
                    ZoneId = currentChallange.ZoneId

                };
                updatedChallenges.Add(orderChallenge);

            }

            order.Challenges = updatedChallenges;
            ConfigureService.CertRecordCache.Add(order.OrderId, order);
            return true;
        }



        public async Task<bool> FinishManualRenewCertRecordById(string orderId)
        {
            ConfigureService.CertRecordCache.TryGetValue(orderId, out CertRecord order);

            if (order == null || order.Challenges.Count == 0)
            {
                await _logger.Error($"[{orderId}]: No ACME service found for Order ID: {order.OrderId}");

            }
            if (string.IsNullOrWhiteSpace(orderId) || string.IsNullOrWhiteSpace(order.UserId))
            {
                await _logger.Error($"[{orderId}]: No Order ID found: {order.OrderId}");
                return false;
            }
            const int maxAttempts = 5;
            int attempt = 0;
            ConfigureService.AcmeServiceCache.TryGetValue(orderId, out AcmeService ACME);
            if (ACME == null)
            {
                await _logger.Error($"[{orderId}]: No ACME service found in cache");
                return false;
            }

            User user = await _userRepository.GetUserByIdAsync(order.UserId);
            if (user == null)
            {
                await _logger.Error($"[{order.UserId}]: User not found for order ID: {orderId}");
                return false;
            }

            string username = user.Username;
            while (attempt < maxAttempts)
            {
                await _logger.Info($"[{username}]: Attempting DNS verification (try {attempt + 1} of {maxAttempts})...");

                List<(AcmeChallenge challange, bool verified)> challengesResult;
                try
                {
                    challengesResult = await ACME.CheckTXTRecordMultipleDNS(order.Challenges, username);

                }
                catch (Exception ex)
                {
                    await _logger.Error($"[{username}]: DNS verification failed: {ex.Message}");
                    await _logger.Debug($"[{username}]: DNS verification failed: {ex.Message}");
                    attempt++;
                    if (attempt < maxAttempts)
                    {
                        await _logger.Info($"[{username}]: Retrying in 15 seconds... (attempt {attempt + 1} of {maxAttempts})");
                        await Task.Delay(15000);
                    }
                    continue;
                }

                bool allVerified = true;
                List<AcmeChallenge> failedChallenges = new();
                foreach (var result in challengesResult)
                {
                    if (!result.verified)
                    {
                        allVerified = false;

                        failedChallenges.Add(result.challange);
                    }
                }

                if (allVerified)
                {
                    await _logger.Update($"[{username}]: DNS verification successful! Starting certificate generation...");
                    foreach (var _challenge in order.Challenges)
                    {
                        _challenge.Status = "Valid";
                    }

                    await ACME.ProcessCertificateGeneration(order.UseSeparateFiles, order.SavePath, order.Challenges, username);
                    if (order.SaveForRenewal)
                    {



                        await _logger.Update($"[{username}]: Saving order for renewal!");

                        await CertRepository.UpdateCertRecord(order);

                        UserStat stats = await _userRepository.GetUserStatByIdAsync(order.UserId);

                        if (stats == null)
                        {
                            stats = new UserStat
                            {
                                UserId = order.UserId,
                                TotalCerts = 1,
                                CertsRenewed = 1,
                                CertCreationsFailed = 0,
                                LastCertCreated = DateTime.UtcNow
                            };
                        }
                        else
                        {
                            stats.CertsRenewed++;
                            stats.LastCertCreated = DateTime.UtcNow;
                        }

                    }
                    else
                    {
                        foreach (var __challenge in failedChallenges)
                        {
                            __challenge.Status = "Invalid";
                        }

                    }


                    if (!order.UseSeparateFiles)
                    {
                        await _logger.Update($"[{username}]: Certificate stored successfully!");
                    }
                    else
                    {
                        await _logger.Update($"[{username}]: Certificates stored successfully!");
                    }

                    
                }
                else
                {
                    foreach (var failed in failedChallenges)
                    {
                        await _logger.Debug($"[{username}]: DNS verification failed for domain: {failed.Domain}");
                        await _logger.Debug($"[{username}]: Expected TXT record at: _acme-challenge.{failed.Domain}");
                        await _logger.Debug($"[{username}]: Expected value: {failed.DnsChallengeToken}");

                    }
                }

                attempt++;
                if (attempt < maxAttempts)
                {
                    await Task.Delay(15000);
                }
            }

            await _logger.Error($"[{username}]: All {maxAttempts} attempts failed.");
            return true;
        }
            
    }
}


    

