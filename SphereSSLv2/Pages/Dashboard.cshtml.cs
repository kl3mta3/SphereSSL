using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SphereSSLv2.Data;
using SphereSSL2.Model;
using System.DirectoryServices.ActiveDirectory;
using Newtonsoft.Json;
using ACMESharp.Crypto.JOSE;
using Org.BouncyCastle.Crypto;
using System.Security.Cryptography;
using Microsoft.VisualBasic.ApplicationServices;

namespace SphereSSLv2.Pages
{
    public class DashboardModel : PageModel
    {
        private readonly ILogger<DashboardModel> _logger;

        public List<CertRecord> CertRecords = Spheressl.CertRecords;
        public List<CertRecord> ExpiringSoonRecords = Spheressl.ExpiringSoonCertRecords;
        [BindProperty]
        public CertRecord Order { get; set; } = new CertRecord();

        public DashboardModel(ILogger<DashboardModel> logger)
        {
            _logger = logger;
            
        }

        public async Task<IActionResult> OnGet()
        {
            if (Spheressl.UseLogOn)
            {
                var loggedIn = HttpContext.Session.GetString("IsLoggedIn");

                if (loggedIn != "true")
                {
                    return RedirectToPage("/Index");
                }
            }
            CertRecords = Spheressl.CertRecords;
            ExpiringSoonRecords = Spheressl.ExpiringSoonCertRecords;
            return Page();
        }

        public async Task<IActionResult> OnPostQuickCreate([FromBody] CertRecord order)
        {

            AcmeService._acmeService = new AcmeService();
            string orderID = AcmeService.GenerateCertRequestId();
            var (dnsChallengeToken, domain) = await AcmeService._acmeService.CreateUserAccountForCert(order.Email, order.Domain);


            order.OrderId = orderID;
            order.DnsChallengeToken = dnsChallengeToken;
            order.Domain = domain;
            order.ChallengeType = "DNA-01";
            return new JsonResult(order);
        }

        public async Task<IActionResult> OnPostShowChallangeModal([FromBody] CertRecord order)
        {

            var (provider, link) = await Spheressl.GetNameServersProvider(order.Domain);
            var nsList = await Spheressl.GetNameServers(order.Domain);

            using SHA256 algor = SHA256.Create();
            var thumbprintBytes = JwsHelper.ComputeThumbprint(AcmeService._signer, algor);
            var thumbprint = AcmeService.Base64UrlEncode(thumbprintBytes);

            order.Provider = provider;
            order.Signer = AcmeService._signer.Export();
            order.Thumbprint = thumbprint;
            order.AccountID = AcmeService._account.Kid;
            order.OrderUrl = AcmeService._order.OrderUrl;
            order.AuthorizationUrls = AcmeService._order.Payload.Authorizations.ToList();
            order.CreationDate = DateTime.UtcNow;
            order.ExpiryDate = DateTime.UtcNow.AddDays(90);
            string fullLink = "https://" + link;
            string fullDomainName = "_acme-challenge." + order.Domain;

            try
            {
                var html = $@"
                    <form id='showChallangeForm' class='p-4 rounded shadow-sm bg-white border' style='max-width: 650px; min-width: 400px; margin: auto;'>
                        <h3 class='mb-4 text-center text-primary fw-bold'>Add DNS Challenge</h3>

                       <div class='mb-3'>
                            <label class='form-label fw-bold'>Domain Name Server(DNS):</label>
                         <div class='form-control text-break px-3 py-2 bg-light border'>
                             <div> Domain: <a href='{order.Domain}' target='_blank' class='ms-2 text-primary text-decoration-underline'>
                                {order.Domain} </a>  </div>   
                            
                           <div> Provider: {Spheressl.CapitalizeFirstLetter(order.Provider)} </div>
                           <div> Website: <a href='{fullLink}' target='_blank' class='ms-2 text-primary text-decoration-underline'>
                                ({fullLink}) </a> </div>
                                <div> NameServer1: {nsList[0]} </div>  
                                <div> NameServer2: {nsList[1]} </div>  
                             
                        </div>
                        </div>

                        <div class='mb-3'>
                            <p class='mb-1'>Add this to a new TXT record on your DNS:</p>
                            <div class='bg-white border rounded p-3'>
                         
                              <div class='d-flex align-items-center'>
                                    <strong class='me-2'>Name:</strong>
                                    <span id='domainName' class='text-monospace flex-grow-1'>{fullDomainName}</span>
                                    <button type='button' class='btn btn-sm btn-outline-secondary ms-2' onclick='copyDNSName(this)' title='Copy to clipboard'>
                                        <i class=""bi bi-clipboard""></i>
                                    </button>
                                </div>
                                <div class='d-flex align-items-center'>
                                    <strong class='me-2'>Value:</strong>
                                    <span id='dnsToken' class='text-monospace flex-grow-1'>{order.DnsChallengeToken}</span>
                                    <button type='button' class='btn btn-sm btn-outline-secondary ms-2' onclick='copyDnsToken(this)' title='Copy to clipboard'>
                                        <i class=""bi bi-clipboard""></i>
                                    </button>
                                </div>

                            </div>
                        </div>

                        <div class='mb-4' justify-content-center>
                            <p class='mb-0'>Once you've added the record, click <strong>Ready</strong>.</p>
                            <small class='text-muted'>Need help? Click <strong>Learn More</strong>.</small>
                        </div>

                        <div class='d-flex justify-content-end gap-2'>
                            <button type='button' class='btn btn-outline-info' onclick='learnMore()'>Learn More</button>
                            <button type='button' class='btn btn-success' onclick='verifyChallange()'>Ready</button>
                        </div>
                    </form>";

                return Content(html, "text/html");
            }
            catch (Exception ex)
            {
                return Content("<p class='text-danger'>An error occurred while creating the challenge.</p>", "text/html");
            }
        }

        public async Task<IActionResult> OnPostVerifyChallenge([FromBody] CertRecord order)
        {








            return Page();
        }

        public async Task<IActionResult> OnGetExpiringRecordModal(string orderId)
        {


            var record = Spheressl.ExpiringSoonCertRecords.FirstOrDefault(r => r.OrderId == orderId);

            if (record == null)
            {
                return Content("<p class='text-danger'>No record found with that ID.</p>");
            }
            var fakePath = Path.Combine(
                @"C:\Users\Kenny\Documents",
                "cert3.pem"
            );

            record.SavePath = fakePath;

            var directoryPath = Path.GetDirectoryName(record.SavePath)?.Replace("\\", "/") ?? string.Empty;
            var htmlSafePath = System.Net.WebUtility.HtmlEncode(record.SavePath);
            var fileUrl = "file:///" + directoryPath.Replace(" ", "%20");
         

            var html = $@"
        <div class='container-fluid'>
            <div class='row g-3'>
                <div class='col-md-6'><strong>Domain:</strong><br> <a href='{record.Domain}' target='_blank'>{record.Domain}</a></div>
                <div class='col-md-6'><strong>Email:</strong> <span>{record.Email}</span></div>
                <div class='col-md-6'><strong>Provider:</strong> <span>{record.Provider}</span></div>
                <div class='col-md-6'><strong>Challenge Type:</strong> <span>{record.ChallengeType}</span></div>
                <div class='col-md-6'><strong>Created:</strong> <span>{record.CreationDate:g}</span></div>
                <div class='col-md-6'><strong>Expires:</strong> <span>{record.ExpiryDate:g}</span></div>
                <div class='col-md-6'><strong>Auto Renew:</strong> <span class='badge bg-{(record.autoRenew ? "success" : "danger")}'>{(record.autoRenew ? "Enabled" : "Disabled")}</span></div>
                <div class='col-md-6'><strong>Save for Renewal:</strong> <span class='badge bg-{(record.SaveForRenewal ? "info" : "secondary")}'>{(record.SaveForRenewal ? "Yes" : "No")}</span></div>
                <div class='col-md-6'><strong>Successful Renewals:</strong> <span class='text-success fw-bold'>{record.SuccessfulRenewals}</span></div>
                <div class='col-md-6'><strong>Failed Renewals:</strong> <span class='text-danger fw-bold'>{record.FailedRenewals}</span></div>


                <div class='col-md-12'>
                    <strong>Save Path:</strong><br>
                <a href=""#"" onclick=""fetch('http://localhost:7172/open-location/?path=C:/Users/Kenny/Documents'); return false;"">
                    <code class=""small text-break text-danger"">{record.SavePath}</code>
                </a>
                </div>

                <div class='col-md-12'><strong>DNS Token:</strong><br><code class='small text-break'>{record.DnsChallengeToken}</code></div>
                <div class='col-md-12'><strong>Order URL:</strong><br><span>{record.OrderUrl}</span></div>
                <div class='col-md-12'><strong>Account ID:</strong><br><code class='small text-break'>{record.AccountID}</code></div>
            </div>
        </div>";

            return Content(html, "text/html");
        }

        

        public async Task<IActionResult> OnPostShowVerifyModal([FromBody] CertRecord order)
        {








            return Page();
        }
    }
}
