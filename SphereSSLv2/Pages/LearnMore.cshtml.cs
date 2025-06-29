using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Newtonsoft.Json;
using SphereSSLv2.Data.Helpers;
using SphereSSLv2.Models.UserModels;
using SphereSSLv2.Services.Config;

namespace SphereSSLv2.Pages
{
    public class LearnMoreModel : PageModel
    {
        private readonly ILogger<LearnMoreModel> _logger;
        public UserSession CurrentUser = new();

        public LearnMoreModel(ILogger<LearnMoreModel> logger)
        {
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

            return Page();
        }
    }
}
