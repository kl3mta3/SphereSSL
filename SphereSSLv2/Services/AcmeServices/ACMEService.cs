using ACMESharp.Protocol;
using ACMESharp.Protocol.Resources;

using System.Security.Cryptography.X509Certificates;
using System.Net;
using ACMESharp.Crypto;
using System.Net.Http;
using System.Security.Cryptography;
using ACMESharp.Crypto.JOSE.Impl;
using Org.BouncyCastle.Asn1.X509;
using DnsClient;
using System.Threading.Tasks;
using System.IO;
using Certes;
using Certes.Pkcs;
using ACMESharp.Crypto.JOSE;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Text;
using Newtonsoft.Json;
using System.Runtime.Intrinsics.Arm;
using Org.BouncyCastle.Crypto;
using System.Diagnostics;
using SphereSSL2.View;
using SphereSSLv2.Data;
using Microsoft.AspNetCore.SignalR;
using Certes.Acme.Resource;
using Org.BouncyCastle.Tls;
using System;
using SphereSSLv2.Services.Config;


namespace SphereSSLv2.Services.AcmeServices
{
    public class AcmeService
    {
        internal AcmeProtocolClient _client;
        internal ESJwsTool _signer;
        internal  AccountDetails _account;
        internal  ServiceDirectory _directory;
        internal  OrderDetails _order;
        internal  string _domain;
        internal  string _challangeDomain;
        internal bool _UseStaging = true; // Set to true for testing with Let's Encrypt staging environment
        //internal  AcmeService _acmeService;
        internal  Logger _logger;

        public AcmeService(Logger logger)
        {
            _logger = logger;
            _signer = LoadOrCreateSigner(this);
            //_UseStaging = useStaging;



            string _baseAddress = _UseStaging

                ? "https://acme-staging-v02.api.letsencrypt.org/"
                : "https://acme-v02.api.letsencrypt.org/";

            var http = new HttpClient
                {
                    BaseAddress= new Uri(_baseAddress)
                };

            _client = new AcmeProtocolClient(http, null, null, _signer);
            

        }

        public async Task<bool> InitAsync( string email)
        {
            try
            {
                _directory = await _client.GetDirectoryAsync();
                if (_directory == null)
                {
                    await _logger.Error("Directory fetch failed: _directory is null.");
                    return false;
                }

                _client.Directory = _directory;
                await _client.GetNonceAsync();

                _account = await _client.CreateAccountAsync(
                    new[] { $"mailto:{email}" },
                    termsOfServiceAgreed: true,
                    externalAccountBinding: null,
                    throwOnExistingAccount: false
                );

                _client.Account = _account;

                using var algor = SHA256.Create();
                var thumb = JwsHelper.ComputeThumbprint(_signer, algor);

                return true;
            }
            catch (Exception ex)
            {
                await _logger.Error($"[ERROR] InitAsync failed: {ex.Message}");
                return false;
            }
        }

        public async Task<OrderDetails> BeginOrder(string domain)
        {
            try
            {

                _client.Account = _account;
                return await _client.CreateOrderAsync(new[] { domain });
            }
            catch (Exception ex)
            {
                _ = _logger.Info($"[ERROR] Order creation failed: {ex.Message}");
                _ = _logger.Info($"Error- {ex.StackTrace}");
                return null;
            }
        }

        public async Task<(string Domain, string DnsValue)> GetDnsChallengeToken(OrderDetails order)
        {
            var authz = await _client.GetAuthorizationDetailsAsync(order.Payload.Authorizations[0]);
            var dnsChallenge = authz.Challenges.First(c => c.Type == "dns-01");

       
            using SHA256 algor = SHA256.Create();
            var thumbprintBytes = JwsHelper.ComputeThumbprint(_signer, algor);
            var thumbprint = Base64UrlEncode(thumbprintBytes);
            var keyAuth = $"{dnsChallenge.Token}.{thumbprint}";
            byte[] hash = algor.ComputeHash(Encoding.UTF8.GetBytes(keyAuth));
            string dnsValue = Base64UrlEncode(hash);

            return (authz.Identifier.Value, dnsValue);
        }

        internal static string Base64UrlEncode(byte[] data)
        {
            return Convert.ToBase64String(data)
                .TrimEnd('=')                
                .Replace('+', '-')           
                .Replace('/', '_');
        }

        public async Task<(string Token, string Domain)> CreateUserAccountForCert(string email, string requestDomain)
        {
            _order = new OrderDetails();
            _domain = "";



            if (string.IsNullOrWhiteSpace(requestDomain))
            {
                await _logger.Error("Domain name is empty.");
                return (null, null);
            }
           _domain = requestDomain;


            try
            {

                var account = await InitAsync(email);
                if (!account)
                {

                    _ = _logger.Debug("Account creation failed. Please check your email.");
                    return (null, null);
                }
            }
            catch (Exception ex)
            {
                _ = _logger.Debug("Unexpected error during account creation.");

                _ = _logger.Error(ex.Message);
                _ = _logger.Error(ex.StackTrace);
                return (null, null);
            }

            try
            {

                _order = await BeginOrder(_domain);


                if (_order.Payload.Status == "invalid")
                {
                    _ = _logger.Debug("Order is invalid. Please check your domain.");
                    return (null, null);
                }

            }
            catch (Exception ex)
            {
                _= _logger.Info("Order creation failed. Please check your domain.");
                _= _logger.Info(ex.Message);
                return (null, null);
            }



            var (domain, dnsValue) = await GetDnsChallengeToken(_order);


            return (dnsValue, domain);
        }

       
        internal  async Task<bool> ProcessCertificateGeneration( bool useSeperateFiles, string savePath, string dnsChallengeToken, string domain, string username)
        {

            
            // Generate CSR first
            var key = KeyFactory.NewKey(KeyAlgorithm.RS256);
            var csrBuilder = new CertificationRequestBuilder(key);
            csrBuilder.AddName("CN", _domain);
            csrBuilder.SubjectAlternativeNames.Add(_domain);
            var csr = csrBuilder.Generate();

            _= _logger.Info("Submitting challenge to Let's Encrypt...");

            // Get authorization details
            string authUrl = _order.Payload.Authorizations[0];
            var authz = await _client.GetAuthorizationDetailsAsync(authUrl);
            var dnsChallenge = authz.Challenges.First(c => c.Type == "dns-01");

            _= _logger.Info($"[{username}]: Domain: {authz.Identifier.Value}");
            _= _logger.Info($"[{username}]: Challenge URL: {dnsChallenge.Url}");
            _= _logger.Info($"[{username}]: Challenge status: {dnsChallenge.Status}");

            // Only submit challenge if it's pending
            if (dnsChallenge.Status == "pending")
            {
              
             if (_client.Directory == null || _client.Directory.NewNonce == null)
                {
                    var directory = await _client.GetDirectoryAsync();
                    _client.Directory = directory;
                }

                await _client.GetNonceAsync();
                await _client.AnswerChallengeAsync(dnsChallenge.Url);
                _= _logger.Info("[{username}]: Challenge submitted, waiting for validation...");
            }
            else
            {
                _= _logger.Info($"[{username}]: Challenge already in status: {dnsChallenge.Status}");
              
            }

           
            bool challengeValid = false;
            int maxPollingAttempts = 30; // 30 attempts * 5 seconds = 2.5 minutes max

            for (int i = 0; i < maxPollingAttempts; i++)
            {
                try
                {
                    var updatedAuthz = await _client.GetAuthorizationDetailsAsync(authUrl);
                    var updatedChallenge = updatedAuthz.Challenges.First(c => c.Type == "dns-01");

                    _ = _logger.Info($"[{username}]: Polling ACME challenge validation status...");
                    _ = _logger.Debug($"[{username}]: Polling attempt {i + 1}: Challenge = {updatedChallenge.Status}, Authz = {updatedAuthz.Status}");

                    if (updatedAuthz.Status == "valid" && updatedChallenge.Status == "valid")
                    {
                        challengeValid = true;
                        _= _logger.Info($"[{username}]: Challenge validated successfully!");

                        break;
                    }

                    if (updatedAuthz.Status == "invalid" || updatedChallenge.Status == "invalid")
                    {
                        // Get error details
                        string errorDetail = "Unknown error";
                        if (updatedChallenge.Error != null)
                        {
                            errorDetail = $"{updatedChallenge.Error.ToString()}";
                        }

                        throw new Exception($"Challenge validation failed. Error: {errorDetail}");
                        
                    }

                    if (updatedAuthz.Status == "pending" || updatedChallenge.Status == "pending")
                    {
                        _= _logger.Info($"[{username}]: Still pending, waiting 5 seconds...");
                        await Task.Delay(5000);
                        continue;
                    }

                    // Handle other statuses
                    _= _logger.Info($"[{username}]: Unexpected status - Auth: {updatedAuthz.Status}, Challenge: {updatedChallenge.Status}");
                    await Task.Delay(5000);
                }
                catch (Exception ex)
                {
                    _= _logger.Info($"[{username}]: Error during polling attempt {i + 1}: {ex.Message}");
                    if (i == maxPollingAttempts - 1) throw; // Re-throw on last attempt
                    await Task.Delay(5000);
                }
            }

            if (!challengeValid)
            {
                throw new Exception($"Challenge validation timed out after {maxPollingAttempts} attempts");
               
            }

            _ = _logger.Info($"[{username}]: Finalizing certificate order...");

            // Finalize the order
            await _client.FinalizeOrderAsync(_order.Payload.Finalize, csr);

            // Wait for certificate to be ready
            _= _logger.Info($"[{username}]: Waiting for certificate to be issued...");

            OrderDetails finalizedOrder;
            int certWaitAttempts = 0;
            const int maxCertWaitAttempts = 20;

            do
            {
                await Task.Delay(3000);
                finalizedOrder = await _client.GetOrderDetailsAsync(_order.OrderUrl);
                _= _logger.Info($"[{username}]: Certificate status: {finalizedOrder.Payload.Status}");

                certWaitAttempts++;
                if (certWaitAttempts >= maxCertWaitAttempts)
                {
                    throw new Exception("Certificate issuance timed out");
                }

            } while (finalizedOrder.Payload.Status == "processing");

            if (finalizedOrder.Payload.Status != "valid")
            {
                throw new Exception($"[{username}]: Certificate order failed with status: {finalizedOrder.Payload.Status}");
                
            }

            // Download certificate
            var certUrl = finalizedOrder.Payload.Certificate;
            if (string.IsNullOrEmpty(certUrl))
            {
                throw new Exception("Certificate URL is missing from the finalized order");
                
            }

            _= _logger.Info($"[{username}]: Downloading certificate...");
            using var http = new HttpClient();
            var certPem = await http.GetStringAsync(certUrl);

            await DownloadCertificateAsync(useSeperateFiles,  savePath, certPem, key.ToPem(), username);

            _= _logger.Info($"[{username}]: SSL Certificate successfully generated and downloaded!");
            return true;
        }

        internal async Task<bool> CheckTXTRecordMultipleDNS(string dnsChallengeToken, string domain, string username)
        {
            string fullRecordName = $"{domain}";

       
            var dnsServers = new[]
            {
                IPAddress.Parse("8.8.8.8"), // Google
                IPAddress.Parse("1.1.1.1"), // Cloudflare
                IPAddress.Parse("208.67.222.222"), // OpenDNS
                IPAddress.Parse("9.9.9.9") // Quad9
            };

            foreach (var dnsServer in dnsServers)
            {
                try
                {
                    var lookup = new LookupClient(dnsServer);
                    _= _logger.Info($"[{username}]: Checking DNS server {dnsServer} for TXT record at {fullRecordName}");

                    var result = await lookup.QueryAsync(fullRecordName, QueryType.TXT);
                    var txtRecords = result.Answers.TxtRecords();

                    foreach (var record in txtRecords)
                    {
                        foreach (var txt in record.Text)
                        {
                            _= _logger.Info($"[{username}]: Found TXT record: {txt}");
                            if (txt.Trim('"') == dnsChallengeToken.Trim('"'))
                            {
                                _= _logger.Info($"[{username}]: Match found on DNS server {dnsServer}!");
                                return true;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    await _logger.Info($"[{username}]: DNS server {dnsServer} failed: {ex.Message}");
                    continue; // Try next DNS server
                }
            }

            return false;
        }

        public async Task RequestCertAsync(AcmeService acme, string domain)
        {
            string authUrl = _order.Payload.Authorizations[0];
            ACMESharp.Protocol.Resources.Authorization authz;
            do
            {
                await Task.Delay(2000);
                authz = await _client.GetAuthorizationDetailsAsync(authUrl);
            } while (authz.Status == "pending");

            if (authz.Status != "valid")
            {
                throw new Exception("DNS challenge failed verification.");
            }
        }

        private async Task DownloadCertificateAsync(bool useSeperateFiles, string savePath, string certPem, string keyPem, string username)
        {
    
            _= _logger.Info($"[{username}]: Getting ready for Download  Path:{savePath}!");


            if (Path.GetPathRoot(savePath)?.TrimEnd('\\') == savePath.TrimEnd('\\'))
            {
                _= _logger.Error($"[{username}]: Cannot save directly to the root of a drive. Please choose a subfolder.");
                return;
            }


            if (string.IsNullOrWhiteSpace(savePath))
            {
                savePath = System.IO.Directory.GetCurrentDirectory()+"/certs";
            }
            else if (!Path.IsPathRooted(savePath))
            {
                savePath = Path.Combine(System.IO.Directory.GetCurrentDirectory(), savePath);
            }
            savePath = Path.GetFullPath(savePath);


            System.IO.Directory.CreateDirectory(savePath);

            string certFile = "";
            string keyFile = "";
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string prefix = "cert_" + timestamp;

            try
            {

                if (!useSeperateFiles)
                {

                    string combinedPath = Path.Combine(savePath, $"{prefix}.pem");
                    File.WriteAllText(combinedPath, certPem + "\n" + keyPem);
                    _ = _logger.Info($"[{username}]: Saved combined PEM: {combinedPath}");
                    certFile = certPem + "\n" + keyPem;
                }
                else if (useSeperateFiles)
                {
                    string certPath = Path.Combine(savePath, $"{prefix}.crt");
                    string keyPath = Path.Combine(savePath, $"{prefix}.key");
                    File.WriteAllText(certPath, certPem);
                    File.WriteAllText(keyPath, keyPem);
                    _= _logger.Info($"[{username}]: Saved certificate: {certPath}");
                    _ = _logger.Info($"[{username}]: Saved private key: {keyPath}");
                    certFile = certPem ;
                    keyFile = keyPem;
                }

            }
            catch (Exception ex)
            {
                _ = _logger.Error($"[{username}]: Error saving files: {ex.Message}");
            }

            await Task.Delay(500);
            string tempFolder = Path.Combine(AppContext.BaseDirectory, "Temp");
            System.IO.Directory.CreateDirectory(tempFolder);
           
            try
            {
                if (!useSeperateFiles)
                {
                    string savefile = Path.Combine(tempFolder, $"tempCert.pem");
                    File.WriteAllText(savefile, certFile);

                }
                else
                {
                    string saveCrtfile = Path.Combine(tempFolder, $"tempCert.crt");
                    string saveKeyfile = Path.Combine(tempFolder, $"tempKey.key");

                    File.WriteAllText(saveCrtfile, certFile);
                    File.WriteAllText(saveKeyfile, keyFile);

                }
            }
            catch { /* silently fail if not Windows or explorer not available */ }
        }

        internal static  ESJwsTool LoadOrCreateSigner( AcmeService acme, string path = "signer.pem")
        {
            var signer = new ESJwsTool();

            if (File.Exists(path))
            {
                string pem = File.ReadAllText(path);
                signer.Import(pem); 
            }
            else
            {                
                signer.Init();
                string exported = signer.Export();
                File.WriteAllText(path, exported); 
            }

            acme._signer = signer;
            return signer;
        }

        internal static string GenerateCertRequestId()
        {
            byte[] randomBytes = new byte[32];

            RandomNumberGenerator.Fill(randomBytes);

            return BitConverter.ToString(randomBytes).Replace("-", "").ToLower();
        }
    }
}
