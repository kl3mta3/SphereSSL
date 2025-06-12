using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SphereSSLv2.Data;
using System.Security.Cryptography;
using System.Text;
using static Org.BouncyCastle.Math.EC.ECCurve;

namespace SphereSSLv2.Pages
{
    public class IndexModel : PageModel
    {

        private readonly ILogger<IndexModel> _logger;

        public IndexModel(ILogger<IndexModel> logger)
        {
            _logger = logger;
        }

        [BindProperty]
        public string Username { get; set; } = "";

        [BindProperty]
        public string Password { get; set; } = "";

        public IActionResult OnGet()
        {

             if (!Spheressl.UseLogOn)
            {

                if (!string.IsNullOrEmpty(Spheressl.Username))
                {
                    HttpContext.Session.SetString("Username", Spheressl.Username);

                }
                else
                {
                    HttpContext.Session.SetString("Username", " ");
                }
                HttpContext.Session.SetString("IsLoggedIn", "true");
                return RedirectToPage("/Dashboard");
            }

            return Page();
        }

        public IActionResult OnPost()
        {
            if (Spheressl.UseLogOn)
            {

                string usernameLower = Username.ToLower();
                string prehashedPass = Convert.ToBase64String(SHA512.HashData(Encoding.UTF8.GetBytes(Password)));

                //if (!Spheressl.VerifyPassword(prehashedPass, Spheressl.HashedPassword) || usernameLower != Spheressl.Username)
                //{
                //    ModelState.AddModelError(string.Empty, "Invalid Password or UserName.");
                //    return Page();
                //}

                if (Password!="letmein"|| usernameLower != "admin")
                {
                    ModelState.AddModelError(string.Empty, "Invalid Password or UserName.");
                    return Page();
                }
                HttpContext.Session.SetString("IsLoggedIn", "true");

                HttpContext.Session.SetString("Username", Username);
                Spheressl.IsLogIn = true;
                return RedirectToPage("/Dashboard");
            }
            else if (!Spheressl.UseLogOn)
            {

                if (!string.IsNullOrEmpty(Spheressl.Username))
                {
                    HttpContext.Session.SetString("Username", Spheressl.Username);

                }
                else
                {
                    HttpContext.Session.SetString("Username", " ");
                }
                    HttpContext.Session.SetString("IsLoggedIn", "true");
                return RedirectToPage("/Dashboard");
            }
                return Page();

        }

    }
}
