using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SphereSSLv2.Data.Database;
using SphereSSLv2.Data.Helpers;
using SphereSSLv2.Data.Repositories;
using SphereSSLv2.Models.CertModels;
using SphereSSLv2.Models.DNSModels;
using SphereSSLv2.Models.UserModels;
using SphereSSLv2.Services.Config;

namespace SphereSSLv2.Pages
{
    public class CreateNewModel : PageModel
    {
        private readonly ILogger<CreateNewModel> _ilogger;
        public List<DNSProvider> DNSProviders = new();
        public DNSProvider SelectedDNSProvider { get; set; } = new DNSProvider();
        public UserSession CurrentUser = new();
        public string CAPrimeUrl;
        public string CAStagingUrl;
        private readonly DatabaseManager _databaseManager;
        private readonly ConfigureService _spheressl;
        private readonly UserRepository _userRepository;
        private readonly DnsProviderRepository _dnsProviderRepository;
        private readonly CertRepository _certRepository;
        private readonly Logger _logger;
        public List<CertRecord> CertRecords = new();
        public List<CertRecord> ExpiringSoonRecords = new();
        public List<string> SupportedAutoProviders = Enum.GetValues(typeof(DNSProvider.ProviderType))
            .Cast<DNSProvider.ProviderType>()
            .Select(p => p.ToString())
            .ToList();


        public CreateNewModel(ILogger<CreateNewModel> ilogger, Logger logger, ConfigureService spheressl, DatabaseManager database, CertRepository certRepository, DnsProviderRepository dnsProviderRepository, UserRepository userRepository)
        {
            _ilogger =ilogger;
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



    }
}

