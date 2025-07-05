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
using System;
using System.Diagnostics;
using System.Security.Policy;
using System.Text.Json;
using System.Text.Json.Serialization;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;
using CertRecord = SphereSSLv2.Models.CertModels.CertRecord;

namespace SphereSSLv2.Services.CertServices
{
    public class CertRecordServiceManager
    {
        internal DatabaseManager dbRecord;
        internal DnsProviderRepository _dnsProviderRepository;
        internal UserRepository _userRepository;

        private Logger _logger;
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

        public async Task<bool> RenewCertRecordWithAutoDNSById(Logger logger, string orderId)
        {

            CertRecord order = await CertRepository.GetCertRecordByOrderId(orderId);

            if (order == null)
            {
                await logger.Error($" No certificate record found for order ID: {orderId}");
                return false;
            }

            if (order.autoRenew)
            {
                AcmeService acme = new AcmeService(logger);
                if (acme == null)
                {
                    await logger.Error($"Acme is null");

                }

                ESJwsTool signer = AcmeService.LoadOrCreateSigner(acme);

                if (signer == null)
                {
                    await logger.Error($"[{order.UserId}]:  signer is null: {orderId}");
                    return false;

                }

                _userRepository = new UserRepository(_dnsProviderRepository);
                User user = await _userRepository.GetUserByIdAsync(order.UserId);

                if (user == null)
                {

                    await logger.Error($"[{order.UserId}]: User not found for order ID: {orderId}");
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

                var ACME = new AcmeService(logger)
                {
                    _logger = logger,
                    _signer = signer,
                    _client = new AcmeProtocolClient(http, null, null, signer),


                };
                ConfigureService.AcmeServiceCache.TryAdd(order.OrderId, ACME);

                List<string> domains = order.Challenges.Select(c => c.Domain).ToList();
                var challenges = await ACME.CreateUserAccountForCert(order.Email, domains);

                if (challenges == null)
                {

                    await logger.Error($"[{username}]: Returned domain is null or empty after CreateUserAccountForCert!");

                    return false;
                }
                var updatedChallenges = new List<AcmeChallenge>();

                foreach (var challenge in challenges)
                {
                    AcmeChallenge currentChallange = order.Challenges.FirstOrDefault(c => c.Domain == challenge.Domain);
                    if (currentChallange == null)
                    {

                        await logger.Error($"[{username}]: currentChallange domain:{challenge.Domain} is null or empty!");

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

                foreach (var challenge in order.Challenges)
                {

                    if (string.IsNullOrWhiteSpace(challenge.Domain))
                    {
                        await logger.Error($"[{username}]: Domain is null or empty for auto-adding DNS record.");
                        return false;
                    }
                    _dnsProviderRepository = new DnsProviderRepository();

                    List<DNSProvider> DNSProviders = await _dnsProviderRepository.GetAllDNSProviders();

                    DNSProvider _provider = DNSProviders.FirstOrDefault(p => p.ProviderId.Equals(challenge.ProviderId, StringComparison.OrdinalIgnoreCase));

                    if (_provider == null)
                    {
                        await _logger.Error($"[{username}]: No DNS provider found for {challenge.Domain} (ProviderId: {challenge.ProviderId})");
                        return false;
                    }

                    await logger.Info($"[{username}]: Auto-adding DNS record using provider: {_provider.ProviderName}");

                    var zoneID = await DNSProvider.TryAutoAddDNS(logger, _provider, challenge.Domain, challenge.DnsChallengeToken, username);

                    if (String.IsNullOrWhiteSpace(zoneID))
                    {
                        await logger.Info($"[{username}]: Failed to auto-add DNS record for provider: {_provider}");
                        return false;
                    }
                }


                const int maxAttempts = 5;
                int attempt = 0;
                while (attempt < maxAttempts)
                {
                    await logger.Info($"[{username}]: Attempting DNS verification (try {attempt + 1} of {maxAttempts})...");

                    List<(AcmeChallenge challange, bool verified)> challengesResult;
                    try
                    {

                        challengesResult = await ACME.CheckTXTRecordMultipleDNS(order.Challenges, username);

                    }
                    catch (Exception ex)
                    {
                        await logger.Error($"[{username}]: DNS verification failed: {ex.Message}");
                        await logger.Debug($"[{username}]: DNS verification failed: {ex.Message}");
                        attempt++;
                        if (attempt < maxAttempts)
                        {
                            await logger.Info($"[{username}]: Retrying in 15 seconds... (attempt {attempt + 1} of {maxAttempts})");
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
                        await logger.Update($"[{username}]: DNS verification successful! Starting certificate generation...");
                        foreach (var _challenge in order.Challenges)
                        {
                            _challenge.Status = "Valid";
                        }

                        await ACME.ProcessCertificateGeneration(order.UseSeparateFiles, order.SavePath, order.Challenges, username);


                        if (order.SaveForRenewal)
                        {



                            await logger.Update($"[{username}]: Saving order for renewal!");
                            order.ExpiryDate = DateTime.UtcNow.AddDays(90);
                            order.SuccessfulRenewals++;
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
                            await _userRepository.UpdateUserStatAsync(stats);
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
                            await logger.Update($"[{username}]: Certificate stored successfully!");
                        }
                        else
                        {
                            await logger.Update($"[{username}]: Certificates stored successfully!");
                        }

                        return true;
                    }
                    else
                    {
                        foreach (var failed in failedChallenges)
                        {
                            await logger.Debug($"[{username}]: DNS verification failed for domain: {failed.Domain}");
                            await logger.Debug($"[{username}]: Expected TXT record at: _acme-challenge.{failed.Domain}");
                            await logger.Debug($"[{username}]: Expected value: {failed.DnsChallengeToken}");

                        }
                    }

                    attempt++;
                    if (attempt < maxAttempts)
                    {

                        await Task.Delay(15000);
                    }
                }

                await logger.Error($"[{username}]: All {maxAttempts} attempts failed.");

            }
            else
            {
                await logger.Error($"Certificate with order ID {orderId} is not eligible for renewal.");
                return false;
            }
            return false;
        }

        public async Task<List<AcmeChallenge>> StartManualRenewCertRecordById(Logger logger, string orderId)
        {
            CertRecord order = await CertRepository.GetCertRecordByOrderId(orderId);

            if (order == null)
            {
                await logger.Error($" No certificate record found for order ID: {orderId}");
                return null;
            }


            AcmeService acme = new AcmeService(logger);
            if (acme == null)
            {
                await logger.Error($"Acme is null");
                return null;
            }

            ESJwsTool signer = AcmeService.LoadOrCreateSigner(acme);

            if (signer == null)
            {
                await logger.Error($"[{order.UserId}]:  signer is null: {orderId}");
                return null;

            }

            _userRepository = new UserRepository(_dnsProviderRepository);
            User user = await _userRepository.GetUserByIdAsync(order.UserId);

            if (user == null)
            {

                await logger.Error($"[{order.UserId}]: User not found for order ID: {orderId}");
                return null;
            }

            string username = user.Username;

            var url = order.OrderUrl;

            var uri = new Uri(url);

            var baseUrl = $"{uri.Scheme}://{uri.Host}/";

            var http = new HttpClient
            {
                BaseAddress = new Uri(baseUrl),
            };

            var ACME = new AcmeService(logger)
            {
                _logger = logger,
                _signer = signer,
                _client = new AcmeProtocolClient(http, null, null, signer),


            };
            ConfigureService.AcmeServiceCache.TryAdd(order.OrderId, ACME);

            List<string> domains = order.Challenges.Select(c => c.Domain).ToList();
            var challenges = await ACME.CreateUserAccountForCert(order.Email, domains);

            if (challenges == null)
            {

                await logger.Error($"[{username}]: Returned domain is null or empty after CreateUserAccountForCert!");

                return null;
            }
            var updatedChallenges = new List<AcmeChallenge>();

            foreach (var challenge in challenges)
            {
                AcmeChallenge currentChallange = order.Challenges.FirstOrDefault(c => c.Domain == challenge.Domain);
                if (currentChallange == null)
                {

                    await logger.Error($"[{username}]: currentChallange domain:{challenge.Domain} is null or empty!");

                    return null;
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
            ConfigureService.CertRecordCache.TryAdd(order.OrderId, order);
            return order.Challenges;

        }

        public async Task<bool> FinishManualRenewCertRecordById(Logger logger, string orderId)
        {

            ConfigureService.CertRecordCache.TryGetValue(orderId, out CertRecord order);

            if (order == null || order.Challenges.Count == 0)
            {
                await logger.Error($"[{orderId}]: No ACME service found for Order ID: {order.OrderId}");

            }
            if (string.IsNullOrWhiteSpace(orderId) || string.IsNullOrWhiteSpace(order.UserId))
            {
                await logger.Error($"[{orderId}]: No Order ID found: {order.OrderId}");
                return false;
            }
            const int maxAttempts = 5;
            int attempt = 0;

            ConfigureService.AcmeServiceCache.TryGetValue(orderId, out AcmeService ACME);
            if (ACME == null)
            {
                await logger.Error($"[{orderId}]: No ACME service found in cache");
                return false;
            }

            _userRepository = new UserRepository(_dnsProviderRepository);
            User user = await _userRepository.GetUserByIdAsync(order.UserId);

            if (user == null)
            {
                await logger.Error($"[{order.UserId}]: User not found for order ID: {orderId}");
                return false;
            }

            string username = user.Username;
            while (attempt < maxAttempts)
            {
                await logger.Info($"[{username}]: Attempting DNS verification (try {attempt + 1} of {maxAttempts})...");

                List<(AcmeChallenge challange, bool verified)> challengesResult;
                try
                {

                    challengesResult = await ACME.CheckTXTRecordMultipleDNS(order.Challenges, username);

                }
                catch (Exception ex)
                {
                    await logger.Error($"[{username}]: DNS verification failed: {ex.Message}");
                    await logger.Debug($"[{username}]: DNS verification failed: {ex.Message}");
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
                    await logger.Update($"[{username}]: DNS verification successful! Starting certificate generation...");
                    foreach (var _challenge in order.Challenges)
                    {
                        _challenge.Status = "Valid";
                    }

                    await ACME.ProcessCertificateGeneration(order.UseSeparateFiles, order.SavePath, order.Challenges, username);
                    if (order.SaveForRenewal)
                    {



                        await logger.Update($"[{username}]: Saving order for renewal!");
                        order.SuccessfulRenewals++;
                        order.ExpiryDate = DateTime.UtcNow.AddDays(90);
                        await CertRepository.UpdateCertRecord(order);

                        UserStat stats1 = await _userRepository.GetUserStatByIdAsync(order.UserId);

                        if (stats1 == null)
                        {
                            stats1 = new UserStat
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
                            stats1.CertsRenewed++;
                            stats1.LastCertCreated = DateTime.UtcNow;
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
                        await logger.Update($"[{username}]: Certificate stored successfully!");
                    }
                    else
                    {
                        await logger.Update($"[{username}]: Certificates stored successfully!");
                    }

                    return true;
                }
                else
                {
                    foreach (var failed in failedChallenges)
                    {
                        await logger.Debug($"[{username}]: DNS verification failed for domain: {failed.Domain}");
                        await logger.Debug($"[{username}]: Expected TXT record at: _acme-challenge.{failed.Domain}");
                        await logger.Debug($"[{username}]: Expected value: {failed.DnsChallengeToken}");

                    }
                }

                attempt++;
                if (attempt < maxAttempts)
                {
                    await Task.Delay(15000);
                }
            }

            await logger.Error($"[{username}]: All {maxAttempts} attempts failed.");
            order.FailedRenewals++;
            await CertRepository.UpdateCertRecord(order);

            UserStat stats = await _userRepository.GetUserStatByIdAsync(order.UserId);

            if (stats == null)
            {
                stats = new UserStat
                {
                    UserId = order.UserId,
                    TotalCerts = 1,
                    CertsRenewed = 0,
                    CertCreationsFailed = 1,
                    LastCertCreated = DateTime.UtcNow
                };
            }
            else
            {
                stats.CertCreationsFailed++;
            }


            return true;
        }

        public async Task<bool> RevokeCertRecordByIdAsync(Logger logger, string orderId)
        {
            ConfigureService.CertRecordCache.TryGetValue(orderId, out CertRecord order);

            if (order == null || order.Challenges.Count == 0)
            {
                await logger.Error($"[{orderId}]: No ACME service found for Order ID: {order.OrderId}");

            }
            if (string.IsNullOrWhiteSpace(orderId) || string.IsNullOrWhiteSpace(order.UserId))
            {
                await logger.Error($"[{orderId}]: No Order ID found: {order.OrderId}");
                return false;
            }

            ConfigureService.AcmeServiceCache.TryGetValue(orderId, out AcmeService ACME);
            if (ACME == null)
            {
                await logger.Error($"[{orderId}]: No ACME service found in cache");
                return false;
            }

            _userRepository = new UserRepository(_dnsProviderRepository);
            User user = await _userRepository.GetUserByIdAsync(order.UserId);

            if (user == null)
            {
                await logger.Error($"[{order.UserId}]: User not found for order ID: {orderId}");
                return false;
            }

            string username = user.Username;

            bool success = await ACME.RevokeCert(order);

            return success;


        }

    }
}




