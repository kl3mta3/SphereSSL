using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.VisualBasic.ApplicationServices;
using Newtonsoft.Json;
using SphereSSLv2.Data.Helpers;
using SphereSSLv2.Data.Repositories;
using SphereSSLv2.Models.UserModels;
using SphereSSLv2.Services.Config;
using SphereSSLv2.Services.Security.Auth;
using System.Security.Cryptography;
using System.Text;
using static Org.BouncyCastle.Math.EC.ECCurve;

namespace SphereSSLv2.Pages
{
    public class IndexModel : PageModel
    {

        private readonly ILogger<IndexModel> _logger;
        private readonly UserRepository _userRepository;

        public IndexModel(ILogger<IndexModel> logger, UserRepository userRepository)
        {
            _logger = logger;
            _userRepository = userRepository;
        }

        [BindProperty]
        public string Username { get; set; } = "";

        [BindProperty]
        public string Password { get; set; } = "";

        public async Task<IActionResult> OnGet()
        {

            var random = new Random();
            ViewData["TitleTag"] = SphereSSLTaglines.TaglineArray[random.Next(SphereSSLTaglines.TaglineArray.Length)];


            if (!ConfigureService.UseLogOn)
            {
                // Auto-login logic: fetch admin or default user, set session, then redirect
                string username = ConfigureService.Username ?? "Masterlocke";
                var user = await _userRepository.GetUserByUsernameAsync(username);
                if (user != null)
                {
                    var role = await _userRepository.GetUserRoleByIdAsync(user.UserId);
                    var sessionUser = new UserSession
                    {
                        UserId = user.UserId,
                        Username = user.Username,
                        DisplayName = user.Name,
                        Role = role?.Role ?? "User",
                        IsEnabled = role?.IsEnabled ?? false,
                        IsAdmin = role?.IsAdmin ?? false,
                    };
                    HttpContext.Session.SetString("UserSession", JsonConvert.SerializeObject(sessionUser));
                    return RedirectToPage("/Dashboard");
                }
            }
            return Page();

        }

        public async Task<IActionResult> OnPost()
        {
            string targetUsername = ConfigureService.Username;
            if (string.IsNullOrEmpty(targetUsername))
                targetUsername = "Masterlocke";

            if (ConfigureService.UseLogOn)
            {
          
                HttpContext.Session.SetString("Username", Username);

                var user = await _userRepository.GetUserByUsernameAsync(Username);

                if (user == null)
                {
                    ModelState.AddModelError(string.Empty, "Invalid Username or Password.");
                    return Page();
                }

                // Use case-insensitive compare for safety
                if (!string.Equals(Username, user.Username, StringComparison.OrdinalIgnoreCase))
                {
                    ModelState.AddModelError(string.Empty, "Invalid Username or Password.");
                    return Page();
                }

                string prehashedPass = Convert.ToBase64String(SHA512.HashData(Encoding.UTF8.GetBytes(Password)));

                if (!PasswordService.VerifyPassword(prehashedPass, user.PasswordHash))
                {
                    ModelState.AddModelError(string.Empty, "Invalid Username or Password.");
                    return Page();
                }

                var role = await _userRepository.GetUserRoleByIdAsync(user.UserId);
                var sessionUser = new UserSession
                {
                    UserId = user.UserId,
                    Username = user.Username,
                    DisplayName = user.Name,
                    Role = role?.Role ?? "User",
                    IsEnabled = role?.IsEnabled ?? false,
                    IsAdmin = role?.IsAdmin ?? false,

                };
                HttpContext.Session.SetString("UserSession", JsonConvert.SerializeObject(sessionUser));

                return RedirectToPage("/Dashboard");
            }
            return Page();
        }

        private async Task<bool> SetSessionForUser(string username)
        {
            var user = await _userRepository.GetUserByUsernameAsync(username);

            if (user == null) return false;

            var role = await _userRepository.GetUserRoleByIdAsync(user.UserId);

            var sessionUser = new UserSession
            {
                UserId = user.UserId,
                Username = user.Username,
                DisplayName = user.Name,
                Role = role?.Role ?? "User",
                IsEnabled = role?.IsEnabled ?? false,
                IsAdmin = role?.IsAdmin ?? false,
            };

            HttpContext.Session.SetString("UserSession", JsonConvert.SerializeObject(sessionUser));
            return true;
        }

    }
}
