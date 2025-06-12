using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SphereSSLv2.Data;

namespace SphereSSLv2.Pages
{
    public class DashboardModel : PageModel
    {
        private readonly ILogger<DashboardModel> _logger;


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

            return Page();
        }
    }
}
