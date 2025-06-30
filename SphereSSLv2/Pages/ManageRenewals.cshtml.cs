using ACMESharp.Crypto.JOSE.Impl;
using ACMESharp.Protocol;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SphereSSLv2.Data.Helpers;
using SphereSSLv2.Data.Repositories;
using SphereSSLv2.Models.CertModels;
using SphereSSLv2.Models.DNSModels;
using SphereSSLv2.Models.Dtos;
using SphereSSLv2.Models.UserModels;
using SphereSSLv2.Services.AcmeServices;
using SphereSSLv2.Services.Config;

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

        public ManageRenewalsModel(UserRepository userRepository, Logger logger, DnsProviderRepository dnsProviderRepository, CertRepository certRepository, ILogger<ManageRenewalsModel> ilogger)
        {
            _ilogger = ilogger;
            _userRepository = userRepository;
            _dnsProviderRepository = dnsProviderRepository;
            _certRepository = certRepository;
            _logger = logger;
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


     

    }
}
