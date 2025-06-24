using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Newtonsoft.Json;
using SphereSSLv2.Data;
using SphereSSLv2.Data.Database;
using SphereSSLv2.Models.ConfigModels;
using SphereSSLv2.Models.Dtos;
using SphereSSLv2.Models.UserModels;
using SphereSSLv2.Services.Config;
using SphereSSLv2.Services.Security.Auth;

namespace SphereSSLv2.Pages
{
    public class SettingsModel : PageModel
    {
        private readonly ILogger<SettingsModel> _logger;
        private readonly UserRepository _userRepository;
        private readonly ApiRepository _apiRepository;


        public UserSession CurrentUser = new();
        [BindProperty]
        public string AdminUsername { get; set; } = "";

        [BindProperty]
        public string AdminPassword { get; set; } = "";

        [BindProperty]
        public bool UseLogOn { get; set; } = ConfigureService.UseLogOn;
        [BindProperty]
        public List<User> UserList { get; set; } = new List<User>();

        public SettingsModel(ILogger<SettingsModel> logger, UserRepository userRepository)
        {
            _logger = logger;
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


            UserList = await _userRepository.GetAllUsersAsync();
            return Page();
        }


        public async Task<IActionResult> OnPostToggleLogOnAsync([FromBody] ToggleUseLogOn logOn)
        {


            if (string.IsNullOrWhiteSpace(logOn.Username) || string.IsNullOrWhiteSpace(logOn.Password))
            {
                Console.WriteLine("Admin username or password is empty.");
                ModelState.AddModelError("", "Admin username and password cannot be empty.");
                return Page();
            }

            ConfigureService.UseLogOn = logOn.UseLogOn;
            ConfigureService.Username = logOn.Username;
            ConfigureService.HashedPassword = PasswordService.HashPassword(logOn.Password);

            // Save the updated configuration
            StoredConfig config = new StoredConfig
            {
                UseLogOn = logOn.UseLogOn.ToString(),
                AdminUsername = logOn.Username,
                AdminPassword = logOn.Password
            };

            await ConfigureService.UpdateConfigFile(config);
            return RedirectToPage("/Settings");
        }

        public async Task<IActionResult> OnPostAddUserAsync([FromBody] NewUserRequest userRequest)
        {
            if (string.IsNullOrWhiteSpace(userRequest.Username) || string.IsNullOrWhiteSpace(userRequest.Password))
            {
                ModelState.AddModelError("", "Username and password cannot be empty.");
                return Page();
            }
            try
            {
                var newUser = new User
                {
                    UserId = Guid.NewGuid().ToString("N"),
                    Username = userRequest.Username,
                    PasswordHash = PasswordService.HashPassword(userRequest.Password),
                    Name = userRequest.Name,
                    Email = userRequest.Email,
                    CreationTime = DateTime.UtcNow,
                    LastUpdated = DateTime.UtcNow,
                    UUID = Guid.NewGuid().ToString(), 
                    Notes = userRequest.Notes ?? string.Empty 

                };

                var newRole = new UserRole
                {
                    UserId = newUser.UserId,
                    Role = userRequest.Role ?? "User", // Default to "User" if no role is specified
                    IsAdmin = userRequest.Role?.Equals("Admin", StringComparison.OrdinalIgnoreCase) ?? false,
                    IsEnabled = true // Default to enabled

                };

                var newStat = new UserStat
                {
                    UserId = newUser.UserId,
                    TotalCerts = 0,
                    CertsRenewed = 0, // Initialize login count to 0
                    CertsFailed = 0,
                    LastCertCreated = null,
                };

                await _userRepository.InsertUserintoDatabaseAsync(newUser);
                await _userRepository.InsertUserRoleAsync(newRole);
                await _userRepository.InsertUserStatAsync(newStat);

                return RedirectToPage("/Settings");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error adding user: {ex.Message}");
                return Page();
            }
        }

        public async Task<IActionResult> OnGetViewUserModal(string userId)
        {
            Console.WriteLine($"OnGetViewUserModal triggered for userId: {userId}");
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
            var apiKey = new ApiKey();
            var user = await _userRepository.GetUserByIdAsync(userId);
            var userStat = await _userRepository.GetUserStatByIdAsync(userId);
            var userRole = await _userRepository.GetUserRoleByIdAsync(userId);

            try
            {
                apiKey = await _apiRepository.GetApiKeyByUserIdAsync(userId);
            }
            catch (Exception ex)
            {
                //api key might not exist
            }

            bool isSuperAdmin = CurrentUser.Role.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase);

            if (user == null)
                return NotFound("User not found");

            return new JsonResult(new
            {
                user,
                userStat,
                apiKey,
                isSuperAdmin
            });
        }

    }
}
