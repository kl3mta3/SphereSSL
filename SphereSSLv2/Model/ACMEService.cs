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
using SphereSSLv2.Services;


namespace SphereSSL2.Model
{
    public class AcmeService
    {
        private static AcmeProtocolClient _client;
        private static ESJwsTool _signer;
        private static AccountDetails _account;
        private static ServiceDirectory _directory;
        private static OrderDetails _order;
        private static string _domain;
        private static string _challangeDomain;
        private static bool _UseStaging = false; // Set to true for testing with Let's Encrypt staging environment
        internal static AcmeService _acmeService;

        public AcmeService()
        {
            _signer = LoadOrCreateSigner();



            string _baseAddress = _UseStaging
                ? "https://acme-staging-v02.api.letsencrypt.org/"
                : "https://acme-v02.api.letsencrypt.org/";

            var http = new HttpClient
                {
                    BaseAddress= new Uri(_baseAddress)
                };

            _client = new AcmeProtocolClient(http, null, null, _signer);
        }

        public async Task<bool> InitAsync(string email)
        {
            try
            {
                _directory = await _client.GetDirectoryAsync();
                _client.Directory = _directory;

                await _client.GetNonceAsync();


                var account = await _client.CreateAccountAsync(
                    new[] { $"mailto:{email}" },
                    termsOfServiceAgreed: true,
                    externalAccountBinding: null,
                    throwOnExistingAccount: false
                );

                _account = account;
                _client.Account = account;
                using SHA256 algor = SHA256.Create();
                var thumb = JwsHelper.ComputeThumbprint(_signer, algor);


                return true;
            }
            catch (Exception ex)
            {
                Logger.Debug($"[ERROR] Init failed: {ex.Message}");
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
                Logger.Debug($"[ERROR] Order creation failed: {ex.Message}");
                Logger.Debug($"Error- {ex.StackTrace}");
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

        private static string Base64UrlEncode(byte[] data)
        {
            return Convert.ToBase64String(data)
                .TrimEnd('=')                
                .Replace('+', '-')           
                .Replace('/', '_');
        }

        public async Task<(string Token, string Domain)> CreateUserAccountForCert()
        {
          
            _order = new OrderDetails();
            _domain = "";
     
            string email = "";
            UI.PrintFooter();
            Logger.Debug("Initializing SphereSSL...");

            _domain = Spheressl.PromptFor("Domain");
            email = Spheressl.PromptFor("Email");


            try
            {
                var account = await _acmeService.InitAsync(email);
                if (!account)
                {
                    Logger.Debug("Account creation failed. Please check your email.");
                    return (null, null);
                }
            }
            catch (Exception ex)
            {
                Logger.Debug("Unexpected error during account creation.");
                Logger.Debug(ex.Message);
                return (null, null);
            }

            try
            {
                _order = await _acmeService.BeginOrder(_domain);
                if (_order.Payload.Status == "invalid")
                {
                    Logger.Debug("Order is invalid. Please check your domain.");
                    return (null, null);
                }

            }
            catch (Exception ex)
            {
                Logger.Debug("Order creation failed. Please check your domain.");
                Logger.Debug(ex.Message);
                return (null, null);
            }

        

            var (domain, dnsValue) = await _acmeService.GetDnsChallengeToken(_order);

            return (dnsValue, domain);
        }

        public static async Task CreateCert()
        {
            _acmeService = new AcmeService();
            Console.Clear();
            var (dnsChallengeToken, domain) = await _acmeService.CreateUserAccountForCert();

            Logger.Debug($"DNS Challenge Token: \"{dnsChallengeToken}\"");
            Logger.Debug($"Domain: {domain}");
            Logger.Debug($"Add this TXT record:");
            Logger.Debug($"Name: _acme-challenge.{domain}");
            Logger.Debug($"Value: {dnsChallengeToken}");

            Logger.Debug("\nPlease add the TXT record to your DNS records.\n");
            Logger.Debug("Press 1: If you need help on what that means.");
            Logger.Debug("Press 2: Once you have added the record to your DNS");
            Logger.Debug("Press 3: If you want to exit the program.");

            var choice = Console.ReadLine();
            switch (choice)
            {
                case "1":
                    UI.HowToAddTXTRecord(false);
                    break;

                case "2":


                    await VerifyRecordWithSpinner(dnsChallengeToken, domain);
                    break;

                case "3":
                    Environment.Exit(0);
                    break;

                default:
                    Logger.Debug("Invalid choice. Please try again.");
                    break;
            }

            Logger.Debug("\nPress any key to return to the main menu...");
            Console.ReadKey();
            await Spheressl.MainMenu();
        }

        public static async Task VerifyRecord(string dnsChallengeToken, string domain)
        {
            const int maxAttempts = 3;
            int attempt = 0;

            while (attempt < maxAttempts)
            {
                Console.Clear();
                Logger.Debug($" Attempting DNS verification (try {attempt + 1} of {maxAttempts})...");
                Logger.Debug("Verifying DNS record...");

                bool verified = false;

                try
                {
                    verified = await CheckTXTRecordAsync(dnsChallengeToken, domain);
                }
                catch (Exception ex)
                {
                    Logger.Debug($"\n[ERROR] Verification failed: {ex.Message}");
                    return;
                }

                if (verified)
                {
                    Logger.Debug("\n🎉 Verification successful! Your cert is being generated...");



                    try
                    {
                        Logger.Debug("\n📡 Submitting challenge to Let's Encrypt...");

                        // Step 1: Get auth URL
                        string authUrl = _order.Payload.Authorizations[0];
                        var authz = await _client.GetAuthorizationDetailsAsync(authUrl);
                        var dnsChallenge = authz.Challenges.First(c => c.Type == "dns-01");

                        // Step 2: Submit the challenge
                        await _client.AnswerChallengeAsync(dnsChallenge.Url);

                        // Step 3: Poll until validated
                        ACMESharp.Protocol.Resources.Authorization updatedAuthz;
                        do
                        {
                            await Task.Delay(2000);
                            updatedAuthz = await _client.GetAuthorizationDetailsAsync(authUrl);
                            Logger.Debug($"Challenge status: {updatedAuthz.Status}");
                        } while (updatedAuthz.Status == "pending");

                        if (updatedAuthz.Status != "valid")
                        {
                            throw new Exception($"Challenge failed: {updatedAuthz.Status}");
                        }

                        Logger.Debug("✅ Challenge validated! Finalizing certificate...");

                        // Step 4: Generate CSR (use your key or tool)

                        var key = KeyFactory.NewKey(KeyAlgorithm.RS256);
                        var csrBuilder = new CertificationRequestBuilder(key);
                        csrBuilder.AddName("CN", _domain);
                        csrBuilder.SubjectAlternativeNames.Add(_domain);
                        var csr = csrBuilder.Generate();


                        // Step 5: Finalize order
                        await _client.FinalizeOrderAsync(_order.Payload.Finalize, csr);

                        // Step 6: Wait for cert to be ready
                        OrderDetails finalizedOrder;
                        do
                        {
                            await Task.Delay(2000);
                            finalizedOrder = await _client.GetOrderDetailsAsync(_order.OrderUrl);
                        } while (finalizedOrder.Payload.Status != "valid");

                        // Step 7: Download certificate

                        var certUrl = finalizedOrder.Payload.Certificate;
                        if (string.IsNullOrEmpty(certUrl))
                        {
                            throw new Exception("Certificate URL is missing from the finalized order.");
                        }

                        using var http = new HttpClient();
                        var certPem = await http.GetStringAsync(certUrl);
                        await DownloadCertificateAsync(certPem, key.ToPem());
                    }
                    catch (Exception ex)
                    {
                        Logger.Debug($"[ERROR] Certificate finalization failed: {ex.Message}");
                        return;
                    }

                }

                Logger.Debug("\n❌ Verification failed. DNS record not found or incorrect.");
                Logger.Debug("Make sure the TXT record is saved and includes the quotes!");

                attempt++;

                if (attempt < maxAttempts)
                {
                    Logger.Debug("\nPress any key to retry verification...");
                    Console.ReadKey();
                }
            }

            Logger.Debug("\n💥 All attempts failed. Please double-check your DNS settings and try again later.");
        }



        public static async Task VerifyRecordWithSpinner(string dnsChallengeToken, string domain)
        {
            const int maxAttempts = 5; 
            int attempt = 0;

            while (attempt < maxAttempts)
            {
                Console.Clear();
                UI.PrintFooter();
                Logger.Debug($"Attempting DNS verification (try {attempt + 1} of {maxAttempts})...");

                var cts = new CancellationTokenSource();
                var spinnerTask = UI.ShowSpinnerAsync("Verifying DNS record...", cts.Token);
                await Task.Delay(15000);
                bool verified = false;

                try
                {
                    verified = await CheckTXTRecordMultipleDNS(dnsChallengeToken, domain);

                    cts.Cancel();
                    await spinnerTask;
                }
                catch (Exception ex)
                {
                    cts.Cancel();
                    await spinnerTask;
                    Logger.Debug($"\n[ERROR] DNS verification failed: {ex.Message}");

                    attempt++;
                    if (attempt < maxAttempts)
                    {
                        Logger.Debug($"\nRetrying in 30 seconds... (attempt {attempt + 1} of {maxAttempts})");
                        await Task.Delay(30000);
                    }
                    continue;
                }

                if (verified)
                {
                    Logger.Debug("\nDNS verification successful! Starting certificate generation...");

                    try
                    {
                        await ProcessCertificateGeneration(dnsChallengeToken, domain);
                        return; 
                    }
                    catch (Exception ex)
                    {
                        Logger.Debug($"[ERROR] Certificate generation failed: {ex.Message}");
                        Logger.Debug($"Stack trace: {ex.StackTrace}");

                        // Check if it's a recoverable error
                        if (ex.Message.Contains("urn:ietf:params:acme:error:dns") ||
                            ex.Message.Contains("urn:ietf:params:acme:error:connection"))
                        {
                            Logger.Debug("This appears to be a DNS propagation issue. Retrying...");
                        }
                        else
                        {
                            Logger.Debug("This appears to be a non-recoverable error.");
                            return;
                        }
                    }
                }
                else
                {
                    Logger.Debug($"\nDNS verification failed (attempt {attempt + 1})");
                    Logger.Debug($"Expected TXT record at: _acme-challenge.{domain}");
                    Logger.Debug($"Expected value: {dnsChallengeToken}");
                    Logger.Debug("Make sure:");
                    Logger.Debug("1. The TXT record is correctly added to your DNS");
                    Logger.Debug("2. The record name is exactly: _acme-challenge");
                    Logger.Debug("3. The record value matches exactly (case-sensitive)");
                    Logger.Debug("4. DNS changes have had time to propagate");
                }

                attempt++;

                if (attempt < maxAttempts)
                {
                    Logger.Debug($"\nWaiting 30 seconds before next attempt...");
                    await Task.Delay(30000);
                }
            }

            Logger.Debug($"\nAll {maxAttempts} attempts failed. Please:");
            Logger.Debug("1. Double-check your DNS TXT record");
            Logger.Debug("2. Wait for DNS propagation (can take up to 24 hours)");
            Logger.Debug("3. Try again later");
        }


        private static async Task ProcessCertificateGeneration(string dnsChallengeToken, string domain)
        {
            // Generate CSR first
            var key = KeyFactory.NewKey(KeyAlgorithm.RS256);
            var csrBuilder = new CertificationRequestBuilder(key);
            csrBuilder.AddName("CN", _domain);
            csrBuilder.SubjectAlternativeNames.Add(_domain);
            var csr = csrBuilder.Generate();

            Logger.Debug("Submitting challenge to Let's Encrypt...");
            UI.PrintFooter();
            // Get authorization details
            string authUrl = _order.Payload.Authorizations[0];
            var authz = await _client.GetAuthorizationDetailsAsync(authUrl);
            var dnsChallenge = authz.Challenges.First(c => c.Type == "dns-01");

            Logger.Debug($"Domain: {authz.Identifier.Value}");
            Logger.Debug($"Challenge URL: {dnsChallenge.Url}");
            Logger.Debug($"Challenge status: {dnsChallenge.Status}");

            // Only submit challenge if it's pending
            if (dnsChallenge.Status == "pending")
            {
                await _client.AnswerChallengeAsync(dnsChallenge.Url);
                Logger.Debug("Challenge submitted, waiting for validation...");
            }
            else
            {
                Logger.Debug($"Challenge already in status: {dnsChallenge.Status}");
            }

           
            bool challengeValid = false;
            int maxPollingAttempts = 30; // 30 attempts * 5 seconds = 2.5 minutes max

            for (int i = 0; i < maxPollingAttempts; i++)
            {
                try
                {
                    var updatedAuthz = await _client.GetAuthorizationDetailsAsync(authUrl);
                    var updatedChallenge = updatedAuthz.Challenges.First(c => c.Type == "dns-01");

                    Logger.Debug($"Polling attempt {i + 1}: Authorization status = {updatedAuthz.Status}, Challenge status = {updatedChallenge.Status}");

                    if (updatedAuthz.Status == "valid" && updatedChallenge.Status == "valid")
                    {
                        challengeValid = true;
                        Logger.Debug("Challenge validated successfully!");
                        Console.Clear();
                        UI.PrintFooter();
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
                        Logger.Debug("Still pending, waiting 5 seconds...");
                        await Task.Delay(5000);
                        continue;
                    }

                    // Handle other statuses
                    Logger.Debug($"Unexpected status - Auth: {updatedAuthz.Status}, Challenge: {updatedChallenge.Status}");
                    await Task.Delay(5000);
                }
                catch (Exception ex)
                {
                    Logger.Debug($"Error during polling attempt {i + 1}: {ex.Message}");
                    if (i == maxPollingAttempts - 1) throw; // Re-throw on last attempt
                    await Task.Delay(5000);
                }
            }

            if (!challengeValid)
            {
                throw new Exception($"Challenge validation timed out after {maxPollingAttempts} attempts");
            }

            Logger.Debug("🛠 Finalizing certificate order...");

            // Finalize the order
            await _client.FinalizeOrderAsync(_order.Payload.Finalize, csr);

            // Wait for certificate to be ready
            Logger.Debug("Waiting for certificate to be issued...");
            OrderDetails finalizedOrder;
            int certWaitAttempts = 0;
            const int maxCertWaitAttempts = 20;

            do
            {
                await Task.Delay(3000);
                finalizedOrder = await _client.GetOrderDetailsAsync(_order.OrderUrl);
                Logger.Debug($"Certificate status: {finalizedOrder.Payload.Status}");

                certWaitAttempts++;
                if (certWaitAttempts >= maxCertWaitAttempts)
                {
                    throw new Exception("Certificate issuance timed out");
                }

            } while (finalizedOrder.Payload.Status == "processing");

            if (finalizedOrder.Payload.Status != "valid")
            {
                throw new Exception($"Certificate order failed with status: {finalizedOrder.Payload.Status}");
            }

            // Download certificate
            var certUrl = finalizedOrder.Payload.Certificate;
            if (string.IsNullOrEmpty(certUrl))
            {
                throw new Exception("Certificate URL is missing from the finalized order");
            }

            Logger.Debug("Downloading certificate...");
            using var http = new HttpClient();
            var certPem = await http.GetStringAsync(certUrl);
            await DownloadCertificateAsync(certPem, key.ToPem());

            Logger.Debug("SSL Certificate successfully generated and downloaded!");
        }


        private static async Task<bool> CheckTXTRecordMultipleDNS(string dnsChallengeToken, string domain)
        {
            string fullRecordName = $"_acme-challenge.{domain}";

       
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
                    Logger.Debug($"Checking DNS server {dnsServer} for TXT record at {fullRecordName}");
                    UI.PrintFooter();
                    var result = await lookup.QueryAsync(fullRecordName, QueryType.TXT);
                    var txtRecords = result.Answers.TxtRecords();

                    foreach (var record in txtRecords)
                    {
                        foreach (var txt in record.Text)
                        {
                            Logger.Debug($"Found TXT record: {txt}");
                            // Compare without quotes - DNS might strip them
                            if (txt.Trim('"') == dnsChallengeToken.Trim('"'))
                            {
                                Logger.Debug($"Match found on DNS server {dnsServer}!");
                                return true;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"DNS server {dnsServer} failed: {ex.Message}");
                    continue; // Try next DNS server
                }
            }

            return false;
        }

        public static async Task RequestCertAsync(string domain)
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

       

        private static async Task DownloadCertificateAsync(string certPem, string keyPem)
        {
            UI.PrintFooter();
            Logger.Debug("Certificate is ready!");
            Logger.Debug("How would you like to save it?");
            Logger.Debug("1. Separate files (.crt and .key)");
            Logger.Debug("2. Combined file (.pem with both cert and key)");
            Console.Write("Enter choice (1 or 2): ");
            var fileChoice = Console.ReadLine()?.Trim();

            Console.Write("Enter the folder path to save your certificate files (default: current directory): ");
            var pathChoice = Console.ReadLine()?.Trim();

            if (Path.GetPathRoot(pathChoice)?.TrimEnd('\\') == pathChoice.TrimEnd('\\'))
            {
                Logger.Error("Cannot save directly to the root of a drive. Please choose a subfolder.");
                return;
            }


            if (string.IsNullOrWhiteSpace(pathChoice))
            {
                pathChoice = Directory.GetCurrentDirectory()+"/certs";
            }
            else if (!Path.IsPathRooted(pathChoice))
            {
                pathChoice = Path.Combine(Directory.GetCurrentDirectory(), pathChoice);
            }
            pathChoice = Path.GetFullPath(pathChoice);


            Directory.CreateDirectory(pathChoice);

            try
            {
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string prefix = "cert_" + timestamp + "_";

                if (fileChoice == "2")
                {

                    string combinedPath = Path.Combine(pathChoice, $"{prefix}combined.pem");
                    File.WriteAllText(combinedPath, certPem + "\n" + keyPem);
                    Logger.Debug($"Saved combined PEM: {combinedPath}");

                }
                else if (fileChoice == "1")
                {
                    string certPath = Path.Combine(pathChoice,$"{prefix}certificate.crt");
                    string keyPath = Path.Combine(pathChoice, $"{prefix}private.key");
                    File.WriteAllText(certPath, certPem);
                    File.WriteAllText(keyPath, keyPem);
                    Logger.Debug($"Saved certificate: {certPath}");
                    Logger.Debug($"Saved private key: {keyPath}");
                    await Task.Delay(500);

                }
                else if (fileChoice != "1" && fileChoice != "2")
                {
                    Logger.Error("Invalid choice. Defaulting to combined file (.pem).");
                    fileChoice = "2";
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error saving files: {ex.Message}");
            }

            await Task.Delay(500); // small delay to ensure files are written

            try
            {
                Process.Start("explorer.exe", pathChoice); // Open the folder in Windows Explorer
            }
            catch { /* silently fail if not Windows or explorer not available */ }
        }

        private static async Task<bool> CheckTXTRecordAsync(string dnsChallengeToken, string domain)
        {
            return await CheckTXTRecordMultipleDNS(dnsChallengeToken, domain);
        }

        private static ESJwsTool LoadOrCreateSigner(string path = "signer.pem")
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

            _signer = signer;
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
