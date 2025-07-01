using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.DirectoryServices.ActiveDirectory;
using Newtonsoft.Json;
using ACMESharp.Crypto.JOSE;
using Org.BouncyCastle.Crypto;
using System.Security.Cryptography;
using Microsoft.VisualBasic.ApplicationServices;
using Microsoft.AspNetCore.SignalR;
using DnsClient;
using System.Net;
using ACMESharp.Protocol;
using Certes.Pkcs;
using Certes;
using System.Diagnostics;
using System.IO;
using Org.BouncyCastle.Asn1.Ocsp;
using System.Text.Json;
using ACMESharp.Crypto.JOSE.Impl;
using Microsoft.Extensions.Logging.Console;
using SphereSSLv2.Services.Config;
using SphereSSLv2.Models.Dtos;
using SphereSSLv2.Models.CertModels;
using SphereSSLv2.Models.DNSModels;
using SphereSSLv2.Services.AcmeServices;
using User = SphereSSLv2.Models.UserModels.User;
using SphereSSLv2.Models.UserModels;
using SphereSSLv2.Data.Repositories;
using SphereSSLv2.Data.Helpers;
using SphereSSLv2.Data.Database;
using Certes.Acme.Resource;
using Nager.PublicSuffix;
using Nager.PublicSuffix.RuleProviders;
using JsonSerializer = System.Text.Json.JsonSerializer;
namespace SphereSSLv2.Pages
{
    public class DashboardModel : PageModel
    {
        private readonly ILogger<DashboardModel> _Ilogger;

        public List<CertRecord> CertRecords = new();
        public List<CertRecord> ExpiringSoonRecords = new();
        public List<DNSProvider> DNSProviders= new();
        public List<string> SupportedAutoProviders = Enum.GetValues(typeof(DNSProvider.ProviderType))
            .Cast<DNSProvider.ProviderType>()
            .Select(p => p.ToString())
            .ToList();

        public DNSProvider SelectedDNSProvider { get; set; } = new DNSProvider();
        private bool _runningCertGeneration = false;
        private readonly DatabaseManager _databaseManager;
        private readonly UserRepository _userRepository;
        private readonly DnsProviderRepository _dnsProviderRepository;
        private readonly CertRepository _certRepository;
        private readonly Logger _logger;
        private readonly ConfigureService _spheressl;

        public string CAPrimeUrl;
        public string CAStagingUrl;
        public UserSession CurrentUser = new();

        public DashboardModel(ILogger<DashboardModel> ilogger, Logger logger, ConfigureService spheressl, DatabaseManager database, CertRepository certRepository, DnsProviderRepository dnsProviderRepository, UserRepository userRepository )
        {
            _Ilogger = ilogger;
            _logger = logger;
            _spheressl = spheressl;
            _databaseManager = database;
            _certRepository = certRepository;
            _dnsProviderRepository = dnsProviderRepository;
            _userRepository = userRepository;
        }

        public async Task<IActionResult> OnGet()
        {
            var random = new Random();
            ViewData["TitleTag"] = SphereSSLTaglines.TaglineArray[random.Next(SphereSSLTaglines.TaglineArray.Length)];


            var sessionData = HttpContext.Session.GetString("UserSession");
            
            //if not logged in return
            if (sessionData == null)
            {
              return RedirectToPage("/Index");
            }

            CurrentUser = JsonConvert.DeserializeObject<UserSession>(sessionData);

            if (CurrentUser == null)
            {
                
                return RedirectToPage("/Index");
            }

            bool _isSuperAdmin = string.Equals(CurrentUser.Role, "SuperAdmin", StringComparison.OrdinalIgnoreCase);

            CAPrimeUrl = ConfigureService.CAPrimeUrl;
            CAStagingUrl = ConfigureService.CAStagingUrl;

            if (CurrentUser.IsEnabled && _isSuperAdmin)
            {
                var now = DateTime.UtcNow;
                CertRecords = await CertRepository.GetAllCertRecords();
                ExpiringSoonRecords = CertRecords
                    .FindAll(cert => cert.ExpiryDate >= now && cert.ExpiryDate <= now.AddDays(30));
                DNSProviders = await _dnsProviderRepository.GetAllDNSProviders();
 
            }
            else if (CurrentUser.IsEnabled && !_isSuperAdmin)
            {
                var now = DateTime.UtcNow;
                CertRecords = await _certRepository.GetAllCertsForUserIdAsync(CurrentUser.UserId);
                ExpiringSoonRecords = CertRecords
                    .FindAll(cert => cert.ExpiryDate >= now && cert.ExpiryDate <= now.AddDays(30));
                DNSProviders = await _dnsProviderRepository.GetAllDNSProvidersByUserId(CurrentUser.UserId);
           
            }

            return Page();
        }
    
        public async Task<IActionResult> OnPostQuickCreate([FromBody] QuickCreateRequest request)
        {
            if (!_runningCertGeneration)
            {
                _runningCertGeneration = true;

                if (request == null)
                {
                    await _logger.Error("QuickCreateRequest was null.");
                    return BadRequest("Invalid request payload.");
                }

                var sessionData = HttpContext.Session.GetString("UserSession");
                if (string.IsNullOrEmpty(sessionData))
                    return RedirectToPage("/Index"); // or return an error

                CurrentUser = JsonConvert.DeserializeObject<UserSession>(sessionData);
                if (CurrentUser == null)
                {
                    return RedirectToPage("/Index"); 
                }

                //remove later
                int index = 1;
                foreach (var item in request.Domains)
                {
                    Console.WriteLine($"Incoming Quick Connect domain{index}:{item}");
                    index++;
                }

                var order = request.Order;
                var providerName = request.Provider;
                string orderID = AcmeService.GenerateCertRequestId();
                order.OrderId = orderID;
                order.UserId = CurrentUser.UserId;



                string _baseAddress = request.UseStaging

                     ? ConfigureService.CAStagingUrl
                     : ConfigureService.CAPrimeUrl;

                    var http = new HttpClient
                    {
                        BaseAddress = new Uri(_baseAddress),
                    };


                ESJwsTool signer = AcmeService.LoadOrCreateSigner(new AcmeService(_logger));

                var ACME = new AcmeService(_logger)
                {
                    _logger = _logger,
                    _signer = signer,
                    _client = new AcmeProtocolClient(http, null, null, signer),

                    
                };
               
                ConfigureService.AcmeServiceCache.Add(request.Order.OrderId, ACME);

                DNSProviders = await _dnsProviderRepository.GetAllDNSProvidersByUserId(CurrentUser.UserId);
                DNSProvider provider = DNSProviders.FirstOrDefault(p => p.ProviderName.Equals(providerName, StringComparison.OrdinalIgnoreCase));

                var autoAdd = request.AutoAdd;

                var challenges = await ACME.CreateUserAccountForCert( order.Email, request.Domains);

                foreach (var challenge in challenges)
                {
                    Console.WriteLine($"[Quick Create]: Domain is null or empty for challenge: {challenge.Domain}");
                }

                if ( challenges == null)
                {

                
                    await _logger.Error($"[{CurrentUser.Username}]: Returned domain is null or empty after CreateUserAccountForCert!");

                    return BadRequest("Failed to create ACME order: domain is empty/null.");
                }

                //remove later
                int index2 = 1;
                foreach (var domain in order.Challenges)
                {
                    Console.WriteLine($"[Quick Create]: Adding challenge for domain{index2}: {domain} ");
                }

                foreach (var challenge in challenges)
                {
                      Console.WriteLine($"[Quick Create]: Adding To challenge domain: {challenge.Domain} ");
                    AcmeChallenge orderChallenge = new AcmeChallenge
                    {
                        ChallengeId = new Guid().ToString("N"),
                        OrderId = order.OrderId,
                        UserId = order.UserId,
                        Domain = challenge.Domain,
                        DnsChallengeToken = challenge.DnsChallengeToken,
                        Status = "InProgress",
                        ProviderId = provider?.ProviderId ?? string.Empty,
                        AuthorizationUrl= challenge.AuthorizationUrl

                    };

                    order.Challenges.Add(orderChallenge);
                }   

                order.ChallengeType = "DNS-01";
                string zoneID = String.Empty;
                bool added = false;

                if (autoAdd)
                {
                    foreach (var _challenge in order.Challenges)
                    {
                        if (string.IsNullOrWhiteSpace(_challenge.Domain))
                        {
                            await _logger.Error($"[{CurrentUser.Username}]: Domain is null or empty for auto-adding DNS record.");
                            return BadRequest("Domain is null or empty for auto-adding DNS record.");
                        }
                        DNSProvider _provider = DNSProviders.FirstOrDefault(p => p.ProviderId.Equals(_challenge.ProviderId, StringComparison.OrdinalIgnoreCase));
                        await _logger.Info($"[{CurrentUser.Username}]: Auto-adding DNS record using provider: {_provider.ProviderName}");
                        zoneID = await DNSProvider.TryAutoAddDNS(_logger, _provider, _challenge.Domain, _challenge.DnsChallengeToken, CurrentUser.Username);

                        if (String.IsNullOrWhiteSpace(zoneID))
                        {
                            await _logger.Info($"[{CurrentUser.Username}]: Failed to auto-add DNS record for provider: {_provider}");
                            added = false;
                        }
                        else
                        {
                            added = true;
                            _challenge.ZoneId = zoneID;
                        }

                    }
                }
             
                ConfigureService.CertRecordCache.Add(order.OrderId, order);
                QuickCreateResponse response = new QuickCreateResponse
                {
                    Order = order,
                    AutoAdd = autoAdd,
                    AutoAddedSuccessfully = added,
                };
                return new JsonResult(response);
            }
            else
            {
                await _logger.Error($"[{CurrentUser.Username}]: A certificate generation is already in progress. Please wait until it completes.");
                return BadRequest("A certificate generation is already in progress. Please wait until it completes.");
            }
        }

        public async Task<IActionResult> OnPostShowChallangeModal([FromBody] QuickCreateResponse _order)
        {
            ConfigureService.CertRecordCache.TryGetValue(_order.Order.OrderId, out CertRecord order);

           
            if (order == null)
            {
                await _logger.Error($"[{CurrentUser.Username}]: No ACME service found for Order ID: {_order.Order.OrderId}");
                return BadRequest("ACME service not found for the specified order.");
            }

            ConfigureService.AcmeServiceCache.TryGetValue(order.OrderId, out AcmeService Acme);
            if(Acme == null)
            {
                await _logger.Error($"[{CurrentUser.Username}]: No ACME service found for Order ID: {order.OrderId}");
                return BadRequest("ACME service not found for the specified order.");
            }

            //name server
            List<(string, string, string)> nsDict = new();
            foreach (var challenge in order.Challenges)
            {
                (string provider, string link) = await _spheressl.GetNameServersProvider(challenge.Domain);
                var nsList = await ConfigureService.GetNameServers(challenge.Domain);
                string fullLink = "https://" + link;
                string fullDomainName = "_acme-challenge." + challenge.Domain;


                var ns = (fullLink, fullDomainName, challenge.DnsChallengeToken);
                nsDict.Add(ns);
            }

            using SHA256 algor = SHA256.Create();
                var thumbprintBytes = JwsHelper.ComputeThumbprint(Acme._signer, algor);
                var thumbprint = AcmeService.Base64UrlEncode(thumbprintBytes);
                order.UserId = CurrentUser.UserId;
                order.Signer = Acme._signer.Export();
                order.Thumbprint = thumbprint;
                order.AccountID = Acme._account.Kid;
                order.OrderUrl = Acme._order.OrderUrl;
                order.CreationDate = DateTime.UtcNow;
                order.ExpiryDate = DateTime.UtcNow.AddDays(90);

                if (_order.AutoAdd)
                {
                    try
                    {
                        var html = $@"
                        <form id='showChallangeForm' class='p-4 rounded shadow-sm bg-white border' style='max-width: 650px; min-width: 400px; margin: auto;'>
                            <h3 class='mb-4 text-center text-primary fw-bold'>Add DNS Challenge</h3>
                            <input type='hidden' id='orderId' value='{order.OrderId}' />

                            <div class='mb-3'>
                                <label class='form-label fw-bold'>Domain Name Server(DNS):</label>
                              
                            </div>

                            <div class='challenge-list border rounded p-3 mb-3' style='max-height: 320px; overflow-y: auto; background: #f9f9fa;'>
                        ";

                        foreach (var challenge in order.Challenges)
                        {
                           
                            (string providerLink, string fullDomainName, string dnsToken) = nsDict.FirstOrDefault(x => x.Item2 == $"_acme-challenge.{challenge.Domain}");
                            string ns1 = "—", ns2 = "—";
                            try
                            {
                                var nsList = await ConfigureService.GetNameServers(challenge.Domain);
                                ns1 = nsList.ElementAtOrDefault(0) ?? "—";
                                ns2 = nsList.ElementAtOrDefault(1) ?? "—";
                            }
                            catch { }


                            var ruleProvider = new LocalFileRuleProvider("public_suffix_list.dat");
                            await ruleProvider.BuildAsync();
                            var domainParser = new DomainParser(ruleProvider);
                            var domainInfo = domainParser.Parse(ns1);
                            string strippedProvider = domainInfo.RegistrableDomain;

                        string fullLink = $"https://{challenge.Domain}";
                        html += $@"
                            <div class='mb-3 pb-2 border-bottom'>

                                <div class='mb-1'>
                                     <strong>Domain:</strong> <a href='{fullLink}' target='_blank' class='text-primary text-decoration-underline'>{challenge.Domain}</a>
                                </div>
                                 <div class='mb-1'>
                                 <strong>Provider:</strong> {strippedProvider}  
                                </div>

                                <div><strong>NameServer1:</strong> {ns1}</div>
                                <div><strong>NameServer2:</strong> {ns2}</div>
                                <div class='d-flex align-items-center mt-2'>
                                    <strong class='me-2'>Name:</strong>
                                    <span class='text-monospace flex-grow-1'>{fullDomainName}</span>
                                    <button type='button' class='btn btn-sm btn-outline-secondary ms-2' onclick='navigator.clipboard.writeText(""{fullDomainName}"")' title='Copy to clipboard'>
                                        <i class='bi bi-clipboard'></i>
                                    </button>
                                </div>
                                <div class='d-flex align-items-center mt-1'>
                                    <strong class='me-2'>Value:</strong>
                                    <span class='text-monospace flex-grow-1'>{dnsToken}</span>
                                    <button type='button' class='btn btn-sm btn-outline-secondary ms-2' onclick='navigator.clipboard.writeText(""{dnsToken}"")' title='Copy to clipboard'>
                                        <i class='bi bi-clipboard'></i>
                                    </button>
                                </div>
                            </div>
                            ";
                        }

                        html += @"
                        </div>
                        <div class='mb-4' justify-content-center>
                            <p class='mb-0'>Once you've added all records, click <strong>Ready</strong>.</p>
                            <small class='text-muted'>Need help? Click <strong>Learn More</strong>.</small>
                        </div>
                        <div class='d-flex justify-content-end gap-2'>
                            <button type='button' class='btn btn-outline-info' onclick='learnMore()'>Learn More</button>
                            <button type='button' class='btn btn-success' onclick='verifyChallange()'>Ready</button>
                        </div>
                        </form>
                        ";


                    return Content(html, "text/html");
                }
                    catch (Exception ex)
                    {
                        return Content("<p class='text-danger'>An error occurred while creating the challenge.</p>", "text/html");
                    }


                }
                else
                {

                    try
                    {
                    var html = $@"
                    <form id='showChallangeForm' class='p-4 rounded shadow-sm bg-white border' style='max-width: 650px; min-width: 400px; margin: auto;'>
                        <h3 class='mb-4 text-center text-primary fw-bold'>Add DNS Challenge</h3>
                        <input type='hidden' id='orderId' value='{order.OrderId}' />

                        <div class='mb-3'>
                            <label class='form-label fw-bold'>Domain Name Server(DNS):</label>
                            
                        </div>

                        <div class='challenge-list border rounded p-3 mb-3' style='max-height: 320px; overflow-y: auto; background: #f9f9fa;'>
                    ";

                    foreach (var challenge in order.Challenges)
                    {
         

                        string ns1 = "—", ns2 = "—", fullDomainName = $"_acme-challenge.{challenge.Domain}", dnsToken = challenge.DnsChallengeToken;
                        string fullLink = $"https://{challenge.Domain}";

                        try
                        {
                            var nsList = await ConfigureService.GetNameServers(challenge.Domain);
                            ns1 = nsList.ElementAtOrDefault(0) ?? "—";
                            ns2 = nsList.ElementAtOrDefault(1) ?? "—";
                        }
                        catch { }
                        string[] parts = ns1.Split('.');
                        string strippedProvider = string.Join('.', parts.TakeLast(2));


                        html += $@"
                        <div class='mb-3 pb-2 border-bottom'>
            
                                <div class='mb-1'>
                                     <strong>Domain:</strong> <a href='{fullLink}' target='_blank' class='text-primary text-decoration-underline'>{challenge.Domain}</a>
                                </div>
                                 <div class='mb-1'>
                                 <strong>Provider:</strong> {strippedProvider}  
                                </div>
                            
                            <div><strong>NameServer1:</strong> {ns1}</div>
                            <div><strong>NameServer2:</strong> {ns2}</div>
                            <div class='d-flex align-items-center mt-2'>
                                <strong class='me-2'>Name:</strong>
                                <span class='text-monospace flex-grow-1'>{fullDomainName}</span>
                                <button type='button' class='btn btn-sm btn-outline-secondary ms-2' onclick='navigator.clipboard.writeText(""{fullDomainName}"")' title='Copy to clipboard'>
                                    <i class='bi bi-clipboard'></i>
                                </button>
                            </div>
                            <div class='d-flex align-items-center mt-1'>
                                <strong class='me-2'>Value:</strong>
                                <span class='text-monospace flex-grow-1'>{dnsToken}</span>
                                <button type='button' class='btn btn-sm btn-outline-secondary ms-2' onclick='navigator.clipboard.writeText(""{dnsToken}"")' title='Copy to clipboard'>
                                    <i class='bi bi-clipboard'></i>
                                </button>
                            </div>
                        </div>
                    ";
                    }

                    html += @"
                        </div>
                        <div class='mb-4' justify-content-center>
                            <p class='mb-0'>Once you've added <strong>all records</strong>, click <strong>Ready</strong>.</p>
                            <small class='text-muted'>Need help? Click <strong>Learn More</strong>.</small>
                        </div>
                        <div class='d-flex justify-content-end gap-2'>
                            <button type='button' class='btn btn-outline-info' onclick='learnMore()'>Learn More</button>
                            <button type='button' class='btn btn-success' onclick='verifyChallange()'>Ready</button>
                        </div>
                    </form>
                    ";

                    return Content(html, "text/html");

                }
                    catch (Exception ex)
                    {
                        return Content("<p class='text-danger'>An error occurred while creating the challenge.</p>", "text/html");
                    }
                }

        }

        public async Task<IActionResult> OnPostShowAddProviderModal()
        {

            string optionsHtml = string.Join("", SupportedAutoProviders.Select(p => $"<option value='{p}'>{p}</option>"));

            var html = $@"
            <form id='addProviderForm' class='p-4 bg-white rounded shadow-sm' style='max-width: 500px; margin: auto;'>
                <h4 class='mb-3 text-center text-primary fw-bold'>Add New DNS Provider</h4>

                <div class='mb-3'>
                    <label for='providerName' class='form-label'>Name</label>
                    <input type='text' id='providerName' name='providerName' class='form-control' placeholder='e.g., Cloudflare' required>
                </div>
                

                  <div class='mb-3'>
                    <label for='provider' class='form-label'>Provider</label>
                    <select id='provider' name='provider' class='form-select' required>
                        <option disabled selected value=''>-- Select a Provider --</option>
                        {optionsHtml}
                    </select>
                </div>
                
                <div class='mb-3'>
                    <label for='apiKey' class='form-label'>API Key</label>
                    <input type='text' id='apiKey' name='apiKey' class='form-control' placeholder='Paste your API key here' required>
                </div>

                <div class='mb-3'>
                    <label for='ttl' class='form-label'>TTL (Time to Live)</label>
                    <input type='number' id='ttl' name='ttl' class='form-control' value='120' min='60'>
                </div>

                <div class='d-flex justify-content-end gap-2'>
                    <button type='button' class='btn btn-secondary' data-bs-dismiss='modal'>Cancel</button>
                    <button type='button' class='btn btn-primary' onclick='submitNewProvider()'>Add Provider</button>
                </div>
            </form>";

            return Content(html, "text/html");
        }

        public async Task<IActionResult> OnPostAddDNSProvider([FromBody] DNSProvider provider)
        {

            var sessionData = HttpContext.Session.GetString("UserSession");
            if (string.IsNullOrEmpty(sessionData))
                return RedirectToPage("/Index"); // or return an error

            CurrentUser = JsonConvert.DeserializeObject<UserSession>(sessionData);
            if (CurrentUser == null)
                return RedirectToPage("/Index"); // or return an error

            if (provider == null || string.IsNullOrWhiteSpace(provider.ProviderName) || string.IsNullOrWhiteSpace(provider.APIKey))
            {
                return BadRequest("Invalid provider data.");
            }
            DNSProviders = await _dnsProviderRepository.GetAllDNSProvidersByUserId(CurrentUser.UserId);
            if (DNSProviders.Any(p => p.ProviderName.Equals(provider.ProviderName, StringComparison.OrdinalIgnoreCase)))
            {
                return BadRequest("Provider already exists.");
            }
            // Add the new provider to the list
            DNSProviders.Add(provider);
            provider.ProviderId = Guid.NewGuid().ToString("N");
            await _dnsProviderRepository.InsertDNSProvider(provider, CurrentUser.UserId);


            return new JsonResult(new
            {
                providerId = provider.ProviderId,
                providerName = provider.ProviderName,
                username = CurrentUser.Username
            });
        }

        public async Task<JsonResult> OnGetGetDNSProvidersAsync()
        {
            DNSProviders = ConfigureService.DNSProviders;

            SupportedAutoProviders = Enum.GetValues(typeof(DNSProvider.ProviderType))
            .Cast<DNSProvider.ProviderType>()
            .Select(p => p.ToString())
            .ToList();

            return new JsonResult(ConfigureService.DNSProviders);
  
        }
       
        public async Task<IActionResult> OnGetGetRecordModal(string orderId)
        {
            var record = await CertRepository.GetCertRecordByOrderId(orderId);
            if (record == null)
            {
                return Content("<p class='text-danger'>No record found with that ID.</p>");
            }
            var username = await _userRepository.GetUsernameByIdAsync(record.UserId);
            if (!string.IsNullOrEmpty(username)) { }
            else
            {
                username = "Unknown/Deleted";
            }
            var directoryPath = Path.GetDirectoryName(record.SavePath)?.Replace("\\", "/") ?? string.Empty;
            var htmlSafePath = System.Net.WebUtility.HtmlEncode(record.SavePath);
            var fileUrl = "file:///" + directoryPath.Replace(" ", "%20");
            var escapedPath = Uri.EscapeDataString(fileUrl);
            var openLink = $@"<a href='#' onclick='fetch(""/open-location?path={escapedPath}""); return false;'>
                    <code class='small text-break text-danger'>{htmlSafePath}</code>
                 </a>";

            var html = $@"
            <div class='container-fluid'>
                 <div class='row g-3'>
                    <div class='col-md-6'><strong>Email:</strong> <span>{record.Email}</span></div>
                    <div class='col-md-6'><strong>User:</strong> <span>{username}</span></div>
                    <div class='col-md-6'><strong>Created:</strong> <span>{record.CreationDate:g}</span></div>
                    <div class='col-md-6'>
                        <strong>Expires:</strong> 
                        <span>{record.ExpiryDate:g}</span><br>
                        <small class='text-muted'>({(record.ExpiryDate - DateTime.UtcNow).Days} days remaining)</small>
                    </div>
                    <div class='col-md-6'><strong>Auto Renew:</strong> <span class='badge bg-{(record.autoRenew ? "success" : "danger")}'>{(record.autoRenew ? "Enabled" : "Disabled")}</span></div>
                    <div class='col-md-6'><strong>Save for Renewal:</strong> <span class='badge bg-{(record.SaveForRenewal ? "info" : "secondary")}'>{(record.SaveForRenewal ? "Yes" : "No")}</span></div>
                    <div class='col-md-6'><strong>Successful Renewals:</strong> <span class='text-success fw-bold'>{record.SuccessfulRenewals}</span></div>
                    <div class='col-md-6'><strong>Failed Renewals:</strong> <span class='text-danger fw-bold'>{record.FailedRenewals}</span></div>
                    <div class='col-md-6'><strong>Challenge Type:</strong> <span>{record.ChallengeType}</span></div>

             <div class='col-md-12'>
                <strong>Save Path:</strong><br>
                {openLink}
            </div>

            <div class='col-md-12'>
                <strong>Domains/Providers/DNS Tokens:</strong>
            <div class='border rounded p-3 mb-3' style='max-height: 320px; overflow-y: auto; background: #f9f9fa;'>
            ";

            foreach (var challenge in record.Challenges)
            {
                string ns1 = "—", ns2 = "—", fullDomainName = $"_acme-challenge.{challenge.Domain}", dnsToken = challenge.DnsChallengeToken;
                string fullLink = $"https://{challenge.Domain}";

                try
                {
                    var nsList = await ConfigureService.GetNameServers(challenge.Domain);
                    ns1 = nsList.ElementAtOrDefault(0) ?? "—";
                    ns2 = nsList.ElementAtOrDefault(1) ?? "—";
                }
                catch { }

                html += $@"
                <div class='mb-3 pb-2 border-bottom'>
                    <div>
                        <strong>Domain:</strong> <a href='{fullLink}' target='_blank' class='text-primary text-decoration-underline'>{challenge.Domain}</a>
                        <span class='badge bg-light text-dark ms-2'>{ConfigureService.CapitalizeFirstLetter(challenge.ProviderId)}</span>
                    </div>
                    <div><strong>NameServer1:</strong> {ns1}</div>
                    <div><strong>NameServer2:</strong> {ns2}</div>
                    <div class='d-flex align-items-center mt-2'>
                        <strong class='me-2'>Name:</strong>
                        <span class='text-monospace flex-grow-1'>{fullDomainName}</span>
                        <button type='button' class='btn btn-sm btn-outline-secondary ms-2' onclick='navigator.clipboard.writeText(""{fullDomainName}"")' title='Copy to clipboard'>
                            <i class='bi bi-clipboard'></i>
                        </button>
                    </div>
                    <div class='d-flex align-items-center mt-1'>
                        <strong class='me-2'>Value:</strong>
                        <span class='text-monospace flex-grow-1'>{dnsToken}</span>
                        <button type='button' class='btn btn-sm btn-outline-secondary ms-2' onclick='navigator.clipboard.writeText(""{dnsToken}"")' title='Copy to clipboard'>
                            <i class='bi bi-clipboard'></i>
                        </button>
                    </div>
                </div>
                ";
            }

            html += @"
            </div>
                </div>

                <div class='col-md-12'>
                    <strong>Order URL:</strong><br>
                    <div class='input-group'>
                        <input type='text' id='orderUrlField' class='form-control' value='" + record.OrderUrl + @"' readonly />
                        <button class='btn btn-outline-secondary' type='button' onclick=""copyToClipboard('orderUrlField')"">
                            <i class='bi bi-clipboard'></i>
                        </button>
                    </div>
                </div>

                <div class='col-md-12'>
                    <strong>Account ID:</strong><br>
                    <div class='input-group'>
                        <input type='text' id='accountIdField' class='form-control' value='" + record.AccountID + @"' readonly />
                        <button class='btn btn-outline-secondary' type='button' onclick=""copyToClipboard('accountIdField')"">
                            <i class='bi bi-clipboard'></i>
                        </button>
                    </div>
                </div>

                </div>
            </div>
            ";

            return Content(html, "text/html");
        }

        public async Task<IActionResult> OnGetShowWaitningModal()
        {
           
            
            var random = new Random();
           var phrase = SphereSSLTaglines.TaglineArray[random.Next(SphereSSLTaglines.TaglineArray.Length)];
            

            var html = $@"
            <div id='waitingModalOverlay' style='
                position: fixed;
                top: 0; left: 0;
                width: 100vw;
                height: 100vh;
                background-color: rgba(0,0,0,0.6);
                display: flex;
                justify-content: center;
                align-items: center;
                z-index: 9999;
                flex-direction: column;
            '>

            <div style='position: relative; width: 120px; height: 120px;'>
                <img src='/img/SphereSSL_ICON.png' alt='SSL Icon' style='
                    width: 80px;
                    height: 80px;
                    position: absolute;
                    top: 20px;
                    left: 20px;
                    z-index: 10;
                ' />
                <div class='spinner-ring'></div>
            </div>

            <p style='margin-top: 20px; color: white; font-size: 1.1rem; text-align: center; max-width: 500px;'>{phrase}</p>
            </div>

            <style>
            .spinner-ring {{
                position: absolute;
                top: 0;
                left: 0;
                width: 120px;
                height: 120px;
                border-radius: 50%;
                animation: rotateRing 2s linear infinite;
            }}

            .spinner-ring::before {{
                content: '';
                position: absolute;
                top: 0;
                left: 50%;
                width: 12px;
                height: 12px;
                background-color: #93ca78;
                border-radius: 50%;
                transform: translateX(-50%);
            }}

            @keyframes rotateRing {{
                0% {{ transform: rotate(0deg); }}
                100% {{ transform: rotate(360deg); }}
            }}
            </style>
            ";

                return Content(html, "text/html");
        }

        public async Task<IActionResult> OnPostShowVerifyModal([FromBody] CertRecord _order)
        {
            ConfigureService.CertRecordCache.TryGetValue(_order.OrderId, out CertRecord order);

            if (order == null || order.Challenges.Count == 0)
            {
                await _logger.Error($"[{CurrentUser.Username}]: No ACME service found for Order ID: {_order.OrderId}");
                return BadRequest("ACME service not found for the specified order.");
            }

            foreach (var challenge in order.Challenges)
            {
                Console.WriteLine($"[Start-OnPostShowVerifyModal]: Domain  for challenge: {challenge.Domain}");
            }

            ConfigureService.AcmeServiceCache.TryGetValue(order.OrderId, out AcmeService ACME);

            if (ACME == null)
            {
                await _logger.Error($"[{CurrentUser.Username}]: No ACME service found for Order ID: {order.OrderId}");
                return BadRequest("ACME service not found for the specified order.");
            }
            var sessionData = HttpContext.Session.GetString("UserSession");
            if (string.IsNullOrEmpty(sessionData))
                return RedirectToPage("/Index");

            CurrentUser = JsonConvert.DeserializeObject<UserSession>(sessionData);

            if (CurrentUser == null)
            {
                return RedirectToPage("/Index");
            }

            order.UserId = CurrentUser.UserId;

            var html = $@"
            <form id='showVerifyForm' class='p-4 rounded shadow-sm bg-white border' style='max-width: 650px; min-width: 400px; min-height: 400px; margin: auto;'>
                <h3 class='mb-2 text-center text-primary fw-bold'>Verify DNS Challenge</h3>
                <input type='hidden' id='userId' value='{order.UserId}' />
                <div id='verifyModalBody' class='modal-body'>
                    <div id='signalLogOutput' class='mt-3 p-2 bg-light border rounded text-monospace' style='max-height: 250px; overflow-y: auto;'></div>
                </div>
            </form>";

            _ = Task.Run(async () =>
            {
                const int maxAttempts = 5;
                int attempt = 0;

                while (attempt < maxAttempts)
                {
                    await _logger.Info($"[{CurrentUser.Username}]: Attempting DNS verification (try {attempt + 1} of {maxAttempts})...");

                    List<(AcmeChallenge challange, bool verified)> challengesResult;
                    try
                    {
                        foreach (var challenge in order.Challenges)
                        {
                            Console.WriteLine($"[Just before CheckTXTRecordMultipleDNS]: Domain  for challenge: {challenge.Domain}");
                        }

                        challengesResult = await ACME.CheckTXTRecordMultipleDNS(order.Challenges, CurrentUser.Username);

                        foreach (var challenge in order.Challenges)
                        {
                            Console.WriteLine($"[Just after CheckTXTRecordMultipleDNS]: Domain  for challenge: {challenge.Domain}");
                        }
                    }
                    catch (Exception ex)
                    {
                        await _logger.Error($"[{CurrentUser.Username}]: DNS verification failed: {ex.Message}");
                        await _logger.Debug($"[{CurrentUser.Username}]: DNS verification failed: {ex.Message}");
                        attempt++;
                        if (attempt < maxAttempts)
                        {
                            await _logger.Info($"[{CurrentUser.Username}]: Retrying in 15 seconds... (attempt {attempt + 1} of {maxAttempts})");
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
                        await _logger.Update($"[{CurrentUser.Username}]: DNS verification successful! Starting certificate generation...");

                        foreach (var challenge in order.Challenges)
                        {
                            Console.WriteLine($"[pre ProcessCertificateGeneration]: Domain  for challenge: {challenge.Domain}");
                        }

                        await ACME.ProcessCertificateGeneration(order.UseSeparateFiles, order.SavePath, order.Challenges, CurrentUser.Username);


                        if (order.SaveForRenewal)
                        {



                            await _logger.Update($"[{CurrentUser.Username}]: Saving order for renewal!");
                            order.UserId = CurrentUser.UserId;

                            await CertRepository.InsertCertRecord(order);

                            foreach (var challenge in order.Challenges)
                            {
                                Console.WriteLine($"[OnPostShowVerifyModal]: Inserting challenge: {challenge.ChallengeId}, with Domain: {challenge.Domain}");
                                await CertRepository.InsertAcmeChallengeAsync(challenge);


                            }

                            UserStat stats = await _userRepository.GetUserStatByIdAsync(CurrentUser.UserId);

                            if (stats == null)
                            {
                                stats = new UserStat
                                {
                                    UserId = CurrentUser.UserId,
                                    TotalCerts = 1,
                                    CertsRenewed = 0,
                                    CertCreationsFailed = 0,
                                    LastCertCreated = DateTime.UtcNow
                                };
                            }
                            else
                            {
                                stats.TotalCerts++;
                                stats.LastCertCreated = DateTime.UtcNow;
                            }

                            CertRecords = ConfigureService.CertRecords;
                        }





                        if (!order.UseSeparateFiles)
                        {
                            await _logger.Update($"[{CurrentUser.Username}]: Certificate stored successfully!");
                        }
                        else
                        {
                            await _logger.Update($"[{CurrentUser.Username}]: Certificates stored successfully!");
                        }



                        return;
                    }
                    else
                    {
                        foreach (var failed in failedChallenges)
                        {
                            await _logger.Debug($"[{CurrentUser.Username}]: DNS verification failed for domain: {failed.Domain}");
                            await _logger.Debug($"[{CurrentUser.Username}]: Expected TXT record at: _acme-challenge.{failed.Domain}");
                            await _logger.Debug($"[{CurrentUser.Username}]: Expected value: {failed.DnsChallengeToken}");
                            await _logger.Debug($"[{CurrentUser.Username}]: Make sure:");
                            await _logger.Debug($"[{CurrentUser.Username}]: 1. The TXT record is correctly added to your DNS");
                            await _logger.Debug($"[{CurrentUser.Username}]: 2. The record name is exactly: _acme-challenge");
                            await _logger.Debug($"[{CurrentUser.Username}]: 3. The record value matches exactly (case-sensitive)");
                            await _logger.Debug($"[{CurrentUser.Username}]: 4. DNS changes have had time to propagate");
                        }
                    }

                    attempt++;
                    if (attempt < maxAttempts)
                    {
                        await _logger.Info($"[{CurrentUser.Username}]: Waiting 15 seconds before next attempt...");
                        await Task.Delay(15000);
                    }
                }

                await _logger.Error($"[{CurrentUser.Username}]: All {maxAttempts} attempts failed.");
                await _logger.Debug($"[{CurrentUser.Username}]: All {maxAttempts} attempts failed.");
            });

            _runningCertGeneration = false;

            return Content(html, "text/html");
        }

        public IActionResult OnGetDownloadCertPem(string savePath)
        {
            string file = Path.Combine(AppContext.BaseDirectory, "Temp", $"tempCert.pem");
            if (!System.IO.File.Exists(file))
                return NotFound();
            var bytes = System.IO.File.ReadAllBytes(file);
            System.IO.File.Delete(file);
            return File(bytes, "application/x-pem-file", "certificate.pem");
        }

        public IActionResult OnGetDownloadCertCrt(string savePath)
        {
            string file = Path.Combine(AppContext.BaseDirectory, "Temp", $"tempCert.crt");
          
            if (!System.IO.File.Exists(file))
                return NotFound();

            var bytes = System.IO.File.ReadAllBytes(file);
            System.IO.File.Delete(file);
            return File(bytes, "application/x-x509-ca-cert", "certificate.crt");
        }

        public IActionResult OnGetDownloadCertKey(string savePath)
        {
            string file = Path.Combine(AppContext.BaseDirectory, "Temp", $"tempKey.key");
            if (!System.IO.File.Exists(file))
                return NotFound();

            var bytes = System.IO.File.ReadAllBytes(file);
            System.IO.File.Delete(file);
            return File(bytes, "application/x-pem-key", "private.key");
        }

        public async Task<IActionResult> OnGetGetCurrentUserUsername()
        {

            var sessionData = HttpContext.Session.GetString("UserSession");
            if (string.IsNullOrEmpty(sessionData))
                return new JsonResult("CurrentSessionData is null");

            CurrentUser = JsonConvert.DeserializeObject<UserSession>(sessionData);
            if (CurrentUser == null)
                return new JsonResult("CurrentUser is null");


            return new JsonResult(new { username = CurrentUser.Username });

        }
    }
}
