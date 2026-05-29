using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Newtonsoft.Json;
using SphereSSLv2.Data.Helpers;
using SphereSSLv2.Data.Repositories;
using SphereSSLv2.Models.DNSModels;
using SphereSSLv2.Models.Dtos;
using SphereSSLv2.Models.UserModels;
using SphereSSLv2.Services.CertServices;
using SphereSSLv2.Services.Config;
using CertRecord = SphereSSLv2.Models.CertModels.CertRecord;

namespace SphereSSLv2.Pages
{
    public class ManageRenewalsModel : PageModel
    {
        private readonly ILogger<ManageRenewalsModel> _ilogger;
        private readonly Logger _logger;
        public UserSession CurrentUser = new();
        public List<CertRecord> CertRecords = new();
        public List<CertRecord> ExpiringSoonRecords = new();
        public List<DNSProvider> DNSProviders = new();
        private readonly UserRepository _userRepository;
        private readonly DnsProviderRepository _dnsProviderRepository;
        private readonly CertRepository _certRepository;
        private  ConfigureService _configureService;
        public int RenewalPeriodDays => ConfigureService.RenewBeforeExpiryDays;

        public ManageRenewalsModel(UserRepository userRepository, Logger logger, DnsProviderRepository dnsProviderRepository, CertRepository certRepository, ILogger<ManageRenewalsModel> ilogger, ConfigureService configureService)
        {
            _ilogger = ilogger;
            _userRepository = userRepository;
            _dnsProviderRepository = dnsProviderRepository;
            _certRepository = certRepository;
            _logger = logger;
            _configureService = configureService;
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

            bool _isAdminOrAbove = string.Equals(CurrentUser.Role, "SuperAdmin", StringComparison.OrdinalIgnoreCase) || CurrentUser.IsAdmin;



            var now = DateTime.UtcNow;
            if (_isAdminOrAbove)
            {
                CertRecords = await CertRepository.GetAllCertRecords();
                DNSProviders = await _dnsProviderRepository.GetAllDNSProviders();
            }
            else
            {
                CertRecords = await _certRepository.GetAllCertsForUserIdAsync(CurrentUser.UserId);
                DNSProviders = await _dnsProviderRepository.GetAllDNSProvidersByUserId(CurrentUser.UserId);
            }
            ExpiringSoonRecords = CertRecords
                .FindAll(cert => cert.ExpiryDate >= now && cert.ExpiryDate <= now.AddDays(ConfigureService.RenewBeforeExpiryDays));

            return Page();
        }

        public async Task<IActionResult> OnPostRenewCertificate([FromBody] OrderRenewRequest request)
        {
           
            if (request==null) 
            {
                await _logger.Error($"[{CurrentUser.Username}]: Certificate with ID {request.OrderId} not found.");
                return NotFound($"Request is null.");
            }

            var orderId = request.OrderId;

            if (string.IsNullOrEmpty(orderId))
            {
                await _logger.Error($"[{CurrentUser.Username}]: OrderId is null or empty.");
                return BadRequest("OrderId is null or empty.");
            }
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


            var order = await CertRepository.GetCertRecordByOrderId(orderId);

            if (order == null)
            {
                await _logger.Error($"[{CurrentUser.Username}]: Certificate with ID {orderId} not found.");
                return NotFound($"Certificate not found.");
            }

            try
            {
                
                CertRecordServiceManager certManager = new CertRecordServiceManager();
                if (order.autoRenew)
                {

                    //auto renew
                    foreach (var challenge in order.Challenges)
                    {
                        if (string.IsNullOrWhiteSpace(challenge.ProviderId))
                        {
                            await _logger.Error($"[{CurrentUser.Username}]: A Provider is required to AutoRenew.");
                            return BadRequest($"Provider required for domain: {challenge.Domain}");
                        }
                    }



                    await certManager.RenewCertRecordWithAutoDNSById(_logger, orderId);
                    await _logger.Update($"[{CurrentUser.Username}]: Renewing certificate {order.OrderId} automatically.");

                    ConfigureService.CertRecordCache.Remove(order.OrderId);
                    ConfigureService.AcmeServiceCache.Remove(order.OrderId);
                    return new JsonResult(new
                    {
                        status = "success",
                        autoRenewed = true,
                        order = (CertRecord?)null,
                        message = "Order Auto-Renewed Successfully"
                    });
                }
                else
                {
                   
                    //start manual renew
                    var challanges = await certManager.StartManualRenewCertRecordById(_logger, orderId);
                    order.Challenges = challanges;
                    List<(string, string, string)> nsDict = new();
                    foreach (var challenge in order.Challenges)
                    {
                        
                        _configureService = new ConfigureService(_logger);
                        (string provider, string link) = await _configureService.GetNameServersProvider(challenge.Domain);
                        
                        var nsList = await ConfigureService.GetNameServers(challenge.Domain);
                        string fullLink = "https://" + link;
                        string fullDomainName = "_acme-challenge." + challenge.Domain;


                        var ns = (fullLink, fullDomainName, challenge.DnsChallengeToken);
                        nsDict.Add(ns);
                    }

                    



                    await _logger.Update($"[{CurrentUser.Username}]:Renewing certificate {order.OrderId} manually.");
                    var html = $@"
                    <form id='showChallangeForm' class='p-4 rounded shadow-sm bg-white border' w-100'>
                        <h3 class='mb-4 text-center text-primary fw-bold'>Add DNS Challenge</h3>
                        <input type='hidden' id='orderId' value='{order.OrderId}' />

                        <div class='mb-3'>
                            <label class='form-label fw-bold'>Domain Name Server(DNS):</label>
                            
                        </div>

                        <div class='challenge-list border rounded p-3 mb-3' style='max-height: 320px; overflow-y: auto; background: #f9f9fa;'>
                    ";
                    
                    foreach (var challenge in order.Challenges)
                    {


                        string ns1 = "�", ns2 = "�", fullDomainName = $"_acme-challenge.{challenge.Domain}", dnsToken = challenge.DnsChallengeToken;
                        string fullLink = $"https://{challenge.Domain}";

                        try
                        {
                            var nsList = await ConfigureService.GetNameServers(challenge.Domain);
                            ns1 = nsList.ElementAtOrDefault(0) ?? "�";
                            ns2 = nsList.ElementAtOrDefault(1) ?? "�";
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
                    

                    OrderRenewRequest _request = new OrderRenewRequest();
                    _request.OrderId = order.OrderId;
                    html += $@"
                        </div>
                        <div class='mb-4' justify-content-center>
                            <p class='mb-0'>Once you've added <strong>all records</strong>, click <strong>Ready</strong>.</p>
                            <small class='text-muted'>Need help? Click <strong>Learn More</strong>.</small>
                        </div>
                        <div class='d-flex justify-content-end gap-2'>
                            <button type='button' class='btn btn-outline-info' onclick='learnMore()'>Learn More</button>
                            <button type='button' id='manualReadyBtn' class='btn btn-success' onclick='CheckManualChallenges(""{@order.OrderId}"")'>Ready</button>
                        </div>
                    </form>
                    ";

                    return new JsonResult(new
                    {
                        status = "manual",
                        html = html
                    });
                
            }
        }

            catch (Exception ex)
            {
                await _logger.Error($"[{CurrentUser.Username}]: Exception occurred while renewing certificate {order.OrderId}: {ex.Message}");

                ConfigureService.CertRecordCache.Remove(order.OrderId);
                ConfigureService.AcmeServiceCache.Remove(order.OrderId);
                return StatusCode(500, "An error occurred while renewing the certificate.");
            }
          
        }

        public async Task<IActionResult> OnPostCheckManualChallengesAsync([FromBody] OrderRenewRequest request)
        {
           
            
            
            if (request == null)
            {
                await _logger.Error($"[{CurrentUser.Username}]: Certificate with ID {request.OrderId} not found.");
                return NotFound($"Request is null.");
            }

            var orderId = request.OrderId;

            if (string.IsNullOrEmpty(orderId))
            {
                await _logger.Error($"[{CurrentUser.Username}]: OrderId is null or empty.");
                return BadRequest("OrderId is null or empty.");
            }
            var sessionData = HttpContext.Session.GetString("UserSession");


            if (sessionData == null)
            {
                return RedirectToPage("/Index");
            }

            CurrentUser = JsonConvert.DeserializeObject<UserSession>(sessionData);

            if (CurrentUser == null)
            {

                return RedirectToPage("/Index");
            }


            var order = await CertRepository.GetCertRecordByOrderId(orderId);

            if (order == null)
            {
                await _logger.Error($"[{CurrentUser.Username}]: Certificate with ID {orderId} not found.");
                return NotFound($"Certificate not found.");
            }
            var html = $@"
                <form id='showVerifyForm' class='p-4 rounded shadow-sm bg-white border' style='width:100%;'>
                    <h3 class='mb-2 text-center text-primary fw-bold'>Verify DNS Challenge</h3>
                    <input type='hidden' id='userId' value='{order.UserId}' />
                    <div id='verifyModalBody' class='modal-body'>
                        <div id='signalLogOutput' class='mt-3 p-2 bg-light border rounded text-monospace' style='max-height: 250px; overflow-y: auto;'></div>
                    </div>
                    <div class='modal-footer'>
                        <div id='downloadButton' style='display: none;'>
                            <span class='me-2 text-success fw-semibold'>Certificate saved! Download a copy?</span>
                            <button class='btn btn-primary' type='button' onclick=""window.location.href='/ManageRenewals?handler=DownloadCertPem&savePath=Temp'"">
                                Download PEM
                            </button>
                        </div>
                        <div id='multiDownloadButtons' style='display: none;'>
                            <span class='me-2 text-success fw-semibold'>Certificates saved! Download copies?</span>
                            <a href='/ManageRenewals?handler=DownloadCertKey&savePath=Temp' class='btn btn-primary'>Download KEY</a>
                            <a href='/ManageRenewals?handler=DownloadCertCrt&savePath=Temp' class='btn btn-primary'>Download CRT</a>
                        </div>
                        <button type='button' class='btn btn-secondary' data-bs-dismiss='modal'>Close</button>
                    </div>
                </form>
                ";


            _ = Task.Run(async () => {
                try
                {
                  
                    
                    CertRecordServiceManager certManager = new();
                    bool success = await certManager.FinishManualRenewCertRecordById(_logger, orderId);
                    
                }
                catch (Exception ex)
                {
                    await _logger.Error($"[{CurrentUser.Username}]: Exception occurred while renewing certificate {orderId}: {ex.Message}");
                }
            });

            ConfigureService.CertRecordCache.Remove(order.OrderId);
            ConfigureService.AcmeServiceCache.Remove(order.OrderId);

            return Content(html, "text/html");
        }

        public async Task<IActionResult> OnPostToggleAutoRenew([FromBody] OrderRenewRequest request)
        {

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
            var orderId = request.OrderId;
            if (string.IsNullOrEmpty(orderId))
            {
                await _logger.Error($"[{CurrentUser.Username}]: Certificate ID is null or empty.");
                return BadRequest("Certificate ID is required.");
            }

            var order = await CertRepository.GetCertRecordByOrderId(orderId);
            if (order == null)
            {
                await _logger.Error($"[{CurrentUser.Username}]: Order is null or empty.");
                return BadRequest("An order is required.");

            }

            foreach (var challenge in order.Challenges)
            {
                if (string.IsNullOrWhiteSpace(challenge.ProviderId))
                {
                    await _logger.Error($"[{CurrentUser.Username}]: A Provider is required to AutoRenew.");
                    return BadRequest($"Provider required for domain: {challenge.Domain}");
                }
            }

            if (order.autoRenew)
            {
                order.autoRenew = false;
            }
            else if (!order.autoRenew)
            {
                order.autoRenew = true;
            }
            await CertRepository.UpdateCertRecord(order);

            return new JsonResult(new
            {
                status = "success",
                message = $"Order Auto-Renewed Set to {order.autoRenew.ToString()}"
            });

        }

        public async Task<IActionResult> OnPostRevokeCertificateAsync([FromBody] OrderRenewRequest request)
        {

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
            var orderId = request.OrderId;
            if (string.IsNullOrEmpty(orderId))
            {
                await _logger.Error($"[{CurrentUser.Username}]: Certificate ID is null or empty.");
                return BadRequest("Certificate ID is required.");
            }

            var order = await CertRepository.GetCertRecordByOrderId(orderId);
            if (order == null)
            {
                await _logger.Error($"[{CurrentUser.Username}]: Order is null or empty.");
                return BadRequest("An order is required.");

            }
            ConfigureService.CertRecordCache.TryAdd(order.OrderId, order);
            CertRecordServiceManager certManager = new();
            bool success = await certManager.RevokeCertRecordByIdAsync(_logger, orderId);
            return new JsonResult(new
            {
                status = success ? "success" : "fail",
                autoRenewed = true,
                order = (CertRecord?)null,
                message = success ? "Order Revoked Successfully" : "Order Revocation Failed"
            });
        }

        // SuperAdmin-only: permanently delete a cert and all of its data from the DB.
        // Does NOT attempt an ACME revoke; it just removes the record entirely.
        public async Task<IActionResult> OnPostDeleteCertificateAsync([FromBody] OrderRenewRequest request)
        {
            var sessionData = HttpContext.Session.GetString("UserSession");
            if (sessionData == null) return RedirectToPage("/Index");

            CurrentUser = JsonConvert.DeserializeObject<UserSession>(sessionData);
            if (CurrentUser == null) return RedirectToPage("/Index");

            if (!string.Equals(CurrentUser.Role, "SuperAdmin", StringComparison.OrdinalIgnoreCase))
            {
                await _logger.Error($"[{CurrentUser.Username}]: Only SuperAdmins can delete certificates.");
                return new JsonResult(new { status = "fail", message = "Only SuperAdmins can delete certificates." });
            }

            var orderId = request?.OrderId;
            if (string.IsNullOrEmpty(orderId))
            {
                await _logger.Error($"[{CurrentUser.Username}]: Order ID is null or empty.");
                return BadRequest("Order ID is required.");
            }

            var order = await CertRepository.GetCertRecordByOrderId(orderId);
            if (order == null)
            {
                // Already gone from CertRecords; still clean up any orphaned challenges/cache.
                await CertRepository.DeleteAcmeChallengesByOrderIdAsync(orderId);
                ConfigureService.CertRecordCache.Remove(orderId);
                ConfigureService.AcmeServiceCache.Remove(orderId);
                return new JsonResult(new { status = "success", message = "Certificate already removed." });
            }

            await CertRepository.DeleteAcmeChallengesByOrderIdAsync(orderId);
            await CertRepository.DeleteCertRecordByOrderId(orderId);
            await HealthRepository.AdjustTotalCertsInDB(-1);

            ConfigureService.CertRecordCache.Remove(orderId);
            ConfigureService.AcmeServiceCache.Remove(orderId);

            await _logger.Info($"[{CurrentUser.Username}]: Certificate {orderId} permanently deleted from the database.");

            return new JsonResult(new { status = "success", message = "Certificate deleted." });
        }


        public async Task<IActionResult> OnGetCertApiInfo(string orderId)
        {
            var sessionData = HttpContext.Session.GetString("UserSession");
            if (sessionData == null) return Unauthorized();
            CurrentUser = JsonConvert.DeserializeObject<UserSession>(sessionData);
            if (CurrentUser == null) return Unauthorized();

            if (string.IsNullOrEmpty(orderId))
                return BadRequest("orderId is required.");

            var cert = await CertRepository.GetCertRecordByOrderId(orderId);
            if (cert == null) return NotFound();

            if (!string.Equals(CurrentUser.Role, "SuperAdmin", StringComparison.OrdinalIgnoreCase) && cert.UserId != CurrentUser.UserId)
                return Forbid();

            // Generate a cert-specific API key the first time it's requested
            if (string.IsNullOrEmpty(cert.CertApiKey))
            {
                cert.CertApiKey = GenerateCertApiKey();
                await CertRepository.SaveCertApiKey(orderId, cert.CertApiKey);
            }

            var domain = cert.Challenges?.FirstOrDefault()?.Domain ?? orderId;
            return new JsonResult(new { domain, orderId, certApiKey = cert.CertApiKey });
        }

        public async Task<IActionResult> OnPostRegenerateCertApiKey([FromBody] OrderRenewRequest request)
        {
            var sessionData = HttpContext.Session.GetString("UserSession");
            if (sessionData == null) return Unauthorized();
            CurrentUser = JsonConvert.DeserializeObject<UserSession>(sessionData);
            if (CurrentUser == null) return Unauthorized();

            var cert = await CertRepository.GetCertRecordByOrderId(request.OrderId);
            if (cert == null) return NotFound();
            if (!string.Equals(CurrentUser.Role, "SuperAdmin", StringComparison.OrdinalIgnoreCase) && cert.UserId != CurrentUser.UserId)
                return Forbid();

            var newKey = GenerateCertApiKey();
            await CertRepository.SaveCertApiKey(request.OrderId, newKey);
            return new JsonResult(new { certApiKey = newKey });
        }

        private static string GenerateCertApiKey()
        {
            var bytes = new byte[24];
            System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
            return Convert.ToHexString(bytes).ToLower();
        }

        public async Task<IActionResult> OnGetDownloadCertPemById(string orderId)
        {
            var sessionData = HttpContext.Session.GetString("UserSession");
            if (sessionData == null) return RedirectToPage("/Index");
            CurrentUser = JsonConvert.DeserializeObject<UserSession>(sessionData);
            if (CurrentUser == null) return RedirectToPage("/Index");

            if (string.IsNullOrEmpty(orderId))
                return BadRequest("orderId is required.");

            var cert = await CertRepository.GetCertRecordByOrderId(orderId);
            if (cert == null) return NotFound("Certificate not found.");
            if (string.IsNullOrEmpty(cert.CertPem))
                return NotFound("Certificate content not stored — re-issue the certificate to enable downloads.");

            if (!string.Equals(CurrentUser.Role, "SuperAdmin", StringComparison.OrdinalIgnoreCase) && cert.UserId != CurrentUser.UserId)
                return Forbid();

            var domain = cert.Challenges?.FirstOrDefault()?.Domain?.TrimStart('*').TrimStart('.') ?? orderId;
            return File(System.Text.Encoding.UTF8.GetBytes(cert.CertPem), "application/x-pem-file", $"{domain}.pem");
        }

        public async Task<IActionResult> OnGetDownloadCertKeyById(string orderId)
        {
            var sessionData = HttpContext.Session.GetString("UserSession");
            if (sessionData == null) return RedirectToPage("/Index");
            CurrentUser = JsonConvert.DeserializeObject<UserSession>(sessionData);
            if (CurrentUser == null) return RedirectToPage("/Index");

            if (string.IsNullOrEmpty(orderId))
                return BadRequest("orderId is required.");

            var cert = await CertRepository.GetCertRecordByOrderId(orderId);
            if (cert == null) return NotFound("Certificate not found.");
            if (string.IsNullOrEmpty(cert.CertKey))
                return NotFound("Private key not stored — re-issue the certificate to enable downloads.");

            if (!string.Equals(CurrentUser.Role, "SuperAdmin", StringComparison.OrdinalIgnoreCase) && cert.UserId != CurrentUser.UserId)
                return Forbid();

            var domain = cert.Challenges?.FirstOrDefault()?.Domain?.TrimStart('*').TrimStart('.') ?? orderId;
            return File(System.Text.Encoding.UTF8.GetBytes(cert.CertKey), "application/x-pem-file", $"{domain}.key");
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


        public async Task<IActionResult> OnPostBulkRenewAsync([FromBody] BulkOrderRequest request)
        {
            var sessionData = HttpContext.Session.GetString("UserSession");
            if (sessionData == null) return RedirectToPage("/Index");
            CurrentUser = JsonConvert.DeserializeObject<UserSession>(sessionData);
            if (CurrentUser == null) return RedirectToPage("/Index");
            if (CurrentUser.Role.Equals("Viewer", StringComparison.OrdinalIgnoreCase))
                return new JsonResult(new { success = false, message = "Viewers cannot perform bulk renewals." });

            int succeeded = 0, failed = 0, skipped = 0;
            foreach (var orderId in request.OrderIds)
            {
                try
                {
                    var order = await CertRepository.GetCertRecordByOrderId(orderId);
                    if (order == null) { failed++; continue; }
                    if (!order.autoRenew) { skipped++; continue; }
                    if (order.Challenges.Any(c => string.IsNullOrWhiteSpace(c.ProviderId))) { failed++; continue; }

                    var certManager = new CertRecordServiceManager();
                    await certManager.RenewCertRecordWithAutoDNSById(_logger, orderId);
                    ConfigureService.CertRecordCache.Remove(orderId);
                    ConfigureService.AcmeServiceCache.Remove(orderId);
                    succeeded++;
                }
                catch { failed++; }
            }

            return new JsonResult(new
            {
                succeeded,
                failed,
                skipped,
                message = $"{succeeded} renewed, {skipped} skipped (auto-renew off), {failed} failed."
            });
        }

        public async Task<IActionResult> OnPostBulkToggleAsync([FromBody] BulkOrderRequest request)
        {
            var sessionData = HttpContext.Session.GetString("UserSession");
            if (sessionData == null) return RedirectToPage("/Index");
            CurrentUser = JsonConvert.DeserializeObject<UserSession>(sessionData);
            if (CurrentUser == null) return RedirectToPage("/Index");
            if (CurrentUser.Role.Equals("Viewer", StringComparison.OrdinalIgnoreCase))
                return new JsonResult(new { success = false, message = "Viewers cannot toggle auto-renew." });

            int succeeded = 0, failed = 0;
            foreach (var orderId in request.OrderIds)
            {
                try
                {
                    var order = await CertRepository.GetCertRecordByOrderId(orderId);
                    if (order == null) { failed++; continue; }
                    order.autoRenew = !order.autoRenew;
                    await CertRepository.UpdateCertRecord(order);
                    succeeded++;
                }
                catch { failed++; }
            }

            return new JsonResult(new
            {
                succeeded,
                failed,
                message = $"{succeeded} toggled, {failed} failed."
            });
        }

        public async Task<IActionResult> OnPostBulkRevokeAsync([FromBody] BulkOrderRequest request)
        {
            var sessionData = HttpContext.Session.GetString("UserSession");
            if (sessionData == null) return RedirectToPage("/Index");
            CurrentUser = JsonConvert.DeserializeObject<UserSession>(sessionData);
            if (CurrentUser == null) return RedirectToPage("/Index");
            if (CurrentUser.Role.Equals("Viewer", StringComparison.OrdinalIgnoreCase))
                return new JsonResult(new { success = false, message = "Viewers cannot revoke certificates." });

            int succeeded = 0, failed = 0;
            var certManager = new CertRecordServiceManager();
            foreach (var orderId in request.OrderIds)
            {
                try
                {
                    var order = await CertRepository.GetCertRecordByOrderId(orderId);
                    if (order == null) { failed++; continue; }
                    ConfigureService.CertRecordCache.TryAdd(order.OrderId, order);
                    bool success = await certManager.RevokeCertRecordByIdAsync(_logger, orderId);
                    if (success) succeeded++; else failed++;
                }
                catch { failed++; }
            }

            return new JsonResult(new
            {
                succeeded,
                failed,
                message = $"{succeeded} revoked, {failed} failed."
            });
        }

        public async Task<IActionResult> OnPostExportCertsAsync([FromBody] BulkOrderRequest request)
        {
            var sessionData = HttpContext.Session.GetString("UserSession");
            if (sessionData == null) return Unauthorized();
            CurrentUser = JsonConvert.DeserializeObject<UserSession>(sessionData);
            if (CurrentUser == null) return Unauthorized();

            var certs = new List<CertRecord>();
            foreach (var orderId in request.OrderIds)
            {
                var cert = await CertRepository.GetCertRecordByOrderId(orderId);
                if (cert != null) certs.Add(cert);
            }

            var json = JsonConvert.SerializeObject(certs, Formatting.Indented);
            var bytes = System.Text.Encoding.UTF8.GetBytes(json);
            string filename = $"spheressl-certs-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json";
            return File(bytes, "application/json", filename);
        }

        public async Task<IActionResult> OnPostImportCertsAsync()
        {
            var sessionData = HttpContext.Session.GetString("UserSession");
            if (sessionData == null) return Unauthorized();
            CurrentUser = JsonConvert.DeserializeObject<UserSession>(sessionData);
            if (CurrentUser == null) return Unauthorized();
            if (CurrentUser.Role.Equals("Viewer", StringComparison.OrdinalIgnoreCase))
                return new JsonResult(new { success = false, message = "Viewers cannot import certificates." });

            Request.EnableBuffering();
            string body;
            using (var reader = new StreamReader(Request.Body, leaveOpen: true))
                body = await reader.ReadToEndAsync();

            List<CertRecord> certs;
            try
            {
                certs = JsonConvert.DeserializeObject<List<CertRecord>>(body);
                if (certs == null) return BadRequest("Invalid JSON format.");
            }
            catch
            {
                return BadRequest("Failed to parse JSON file.");
            }

            int imported = 0, skipped = 0, failed = 0;
            foreach (var cert in certs)
            {
                try
                {
                    var existing = await CertRepository.GetCertRecordByOrderId(cert.OrderId);
                    if (existing != null) { skipped++; continue; }
                    await CertRepository.InsertCertRecord(cert);
                    imported++;
                }
                catch { failed++; }
            }

            return new JsonResult(new
            {
                imported,
                skipped,
                failed,
                message = $"{imported} imported, {skipped} already exist (skipped), {failed} failed."
            });
        }
    }
}
