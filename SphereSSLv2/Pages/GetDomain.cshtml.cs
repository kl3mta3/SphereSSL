using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SphereSSLv2.Data;

namespace SphereSSLv2.Pages
{
    public class GetDomainModel : PageModel
    {
        private readonly ILogger<GetDomainModel> _logger;


        public GetDomainModel(ILogger<GetDomainModel> logger)
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
