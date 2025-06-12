using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SphereSSLv2.Data;

namespace SphereSSLv2.Pages
{
    public class LearnMoreModel : PageModel
    {
        private readonly ILogger<LearnMoreModel> _logger;


        public LearnMoreModel(ILogger<LearnMoreModel> logger)
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

            return Page();
        }
    }
}
