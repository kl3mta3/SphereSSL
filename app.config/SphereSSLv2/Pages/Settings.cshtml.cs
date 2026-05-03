using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Newtonsoft.Json;
using SphereSSLv2.Controllers;
using SphereSSLv2.Data.Helpers;
using SphereSSLv2.Data.Repositories;
using SphereSSLv2.Models.ConfigModels;
using SphereSSLv2.Models.ConnectionModels;
using SphereSSLv2.Models.DNSModels;
using SphereSSLv2.Models.Dtos;
using SphereSSLv2.Models.UserModels;
using SphereSSLv2.Services;
using SphereSSLv2.Services.Config;
using SphereSSLv2.Services.Security.Auth;
using System.Text.RegularExpressions;


namespace SphereSSLv2.Pages
{
    public class SettingsModel : PageModel
    {
        private readonly ILogger<SettingsModel> _ilogger;
        private readonly UserRepository _userRepository;
        private readonly ApiRepository _apiRepository;
        private readonly DnsProviderRepository _dnsProviderRepository;
        private readonly ConnectionRepository _connectionRepository;
        private readonly Logger _logger;
        public UserSession CurrentUser = new();
        public List<DNSProvider> DNSProviders = new();
        public List<UserConnection> UserConnections { get; set; } = new();
        public List<string> SupportedAutoProviders = Enum.GetValues(typeof(DNSProvider.ProviderType))
            .Cast<DNSProvider.ProviderType>()
            .Select(p => p.ToString())
            .ToList();

        [BindProperty]
        public string AdminUsername { get; set; } = "";

        [BindProperty]
        public string AdminPassword { get; set; } = "";

        [BindProperty]
        public string ServerUrl { get; set; } = "";

        [BindProperty]
        public int ServerPort { get; set; }

        [BindProperty]
        public string DBPath { get; set; }

        [BindProperty]
        public bool UseLogOn { get; set; } = ConfigureService.UseLogOn;

        [BindProperty]
        public List<User> UserList { get; set; } = new List<User>();

        [BindProperty]
        public string SelectedUser { get; set; }

        [BindProperty]
        public string CAPrimeUrl { get; set; } = ConfigureService.CAPrimeUrl;

        [BindProperty]
        public string CAStagingUrl { get; set; } = ConfigureService.CAStagingUrl;

        public int CertValidityDays => ConfigureService.CertValidityDays;
        public int RenewBeforeExpiryDays => ConfigureService.RenewBeforeExpiryDays;
        public bool StagingOnly => ConfigureService.StagingOnly;
        public bool RestrictViewers => ConfigureService.RestrictViewers;
        public bool HideViewerLogout => ConfigureService.HideViewerLogout;
        public bool DemoLoginEnabled => ConfigureService.DemoLoginEnabled;
        public string DemoUsername => ConfigureService.DemoUsername;
        public string DemoPassword => ConfigureService.DemoPassword;

        public SettingsModel(ILogger<SettingsModel> ilogger, Logger logger, UserRepository userRepository, DnsProviderRepository dnsProviderRepository, ConnectionRepository connectionRepository)
        {
            _ilogger = ilogger;
            _logger = logger;
            _userRepository = userRepository;
            _dnsProviderRepository = dnsProviderRepository;
            _connectionRepository = connectionRepository;
        }

        public async Task<IActionResult> OnGet()
        {
            var random = new Random();
            ViewData["TitleTag"] = SphereSSLTaglines.TaglineArray[random.Next(SphereSSLTaglines.TaglineArray.Length)];

            var sessionData = HttpContext.Session.GetString("UserSession");

            if (sessionData == null)
            {
                return RedirectToPage("/Index");
            }

            CurrentUser = JsonConvert.DeserializeObject<UserSession>(sessionData);


            if (CurrentUser == null)
            {
                return RedirectToPage("/Index");
            }

            if (CurrentUser.IsAdmin)
            {
                UserList = await _userRepository.GetAllUsersAsync();

            }



            if (CurrentUser.Role.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase) || CurrentUser.IsAdmin)
            {
                DNSProviders = await _dnsProviderRepository.GetAllDNSProviders();
            }
            else
            {
                DNSProviders = await _dnsProviderRepository.GetAllDNSProvidersByUserId(CurrentUser.UserId) ?? new List<DNSProvider>();
            }



            ServerPort = ConfigureService.ServerPort;
            ServerUrl = ConfigureService.ServerIP;
            DBPath = ConfigureService.dbPath;
            AdminUsername = ConfigureService.Username;

            UserConnections = await _connectionRepository.GetConnectionsByUserIdAsync(CurrentUser.UserId);

            return Page();
        }

        public async Task<IActionResult> OnPostUpdateLogOnAsync([FromBody] ToggleUseLogOn logOn)
        {

            var sessionData = HttpContext.Session.GetString("UserSession");

            //if not logged in return
            if (sessionData == null)
            {
                return RedirectToPage("/Index");
            }

            CurrentUser = JsonConvert.DeserializeObject<UserSession>(sessionData);


            if (CurrentUser == null || !CurrentUser.Role.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToPage("/Index");
            }

            if (string.IsNullOrWhiteSpace(logOn.Username) || string.IsNullOrWhiteSpace(logOn.Password))
                return new JsonResult(new { success = false, message = "Username and password cannot be empty." });

            string oldUsername = ConfigureService.Username;

            ConfigureService.UseLogOn = logOn.UseLogOn;
            ConfigureService.Username = logOn.Username;
            ConfigureService.HashedPassword = PasswordService.HashPassword(logOn.Password);

            StoredConfig config = new StoredConfig
            {
                UseLogOn = logOn.UseLogOn,
                AdminUsername = logOn.Username,
                AdminPassword = logOn.Password
            };
            await ConfigureService.UpdateConfigFile(config);

            // Also update the DB user so login works with new credentials
            var adminUser = await _userRepository.GetUserByUsernameAsync(oldUsername);
            if (adminUser != null)
            {
                adminUser.Username = logOn.Username;
                adminUser.PasswordHash = PasswordService.HashPassword(logOn.Password);
                adminUser.LastUpdated = DateTime.UtcNow;
                await _userRepository.UpdateUserAsync(adminUser);
            }

            return new JsonResult(new { success = true });
        }

        public async Task<IActionResult> OnPostAddUserAsync([FromBody] NewUserRequest userRequest)
        {

            if (string.IsNullOrWhiteSpace(userRequest.Username) || string.IsNullOrWhiteSpace(userRequest.Password))
            {
                ModelState.AddModelError("", "Username and password cannot be empty.");
                return Page();
            }


            var sessionData = HttpContext.Session.GetString("UserSession");

            //if not logged in return
            if (sessionData == null)
            {
                return RedirectToPage("/Index");
            }

            CurrentUser = JsonConvert.DeserializeObject<UserSession>(sessionData);


            if (CurrentUser == null || !CurrentUser.IsAdmin)
            {
                return RedirectToPage("/Index");
            }

           

            if (
                userRequest.Password.Length < 8 ||
                userRequest.Password.Length > 24 ||
                !Regex.IsMatch(userRequest.Password, "[A-Z]") ||
                !Regex.IsMatch(userRequest.Password, "[a-z]") ||
                !Regex.IsMatch(userRequest.Password, "[0-9]")
            )
            {
                return new JsonResult(new { success = false, message = "Password must be 8�24 characters, and include uppercase, lowercase, and a number." });
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
                    Role = userRequest.Role ?? "User",
                    IsAdmin = userRequest.Role?.Equals("Admin", StringComparison.OrdinalIgnoreCase) ?? false,
                    IsEnabled = true

                };


                var newStat = new UserStat
                {
                    UserId = newUser.UserId,
                    TotalCerts = 0,
                    CertsRenewed = 0,
                    CertCreationsFailed = 0,
                    CertRenewalsFailed = 0,
                    LastCertCreated = null
                };


                await _userRepository.InsertUserintoDatabaseAsync(newUser);

                await _userRepository.InsertUserRoleAsync(newRole);

                await _userRepository.InsertUserStatAsync(newStat);


                return new JsonResult(new { success = true });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error adding user: {ex.Message}");
                return StatusCode(500, $"Error adding user: {ex.Message}");
            }
        }

        public async Task<IActionResult> OnGetViewUserModal(string userId)
        {
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
            var isEnabled = userRole?.IsEnabled ?? false;
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
                userRole,
                apiKey,
                isEnabled,
                isSuperAdmin,

            })
            {

            };
        }

        public async Task<IActionResult> OnPostDeleteCurrentUser(string userId)
        {
            try
            {

                var sessionData = HttpContext.Session.GetString("UserSession");

                //if not logged in return
                if (sessionData == null)
                {
                    return RedirectToPage("/Index");
                }

                CurrentUser = JsonConvert.DeserializeObject<UserSession>(sessionData);


                if (CurrentUser == null || !CurrentUser.Role.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase))
                {
                    return RedirectToPage("/Index");
                }

                if (string.IsNullOrWhiteSpace(userId))
                {
                    return BadRequest("User ID cannot be empty.");
                }

                if (CurrentUser.UserId.Equals(userId, StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest("You cannot delete SuperAdmin accounts.");
                }

                var result = await _userRepository.DeleteUserAsync(userId);
                if (result)
                    return new JsonResult(new { success = true });
                else
                    return StatusCode(500, "Failed to delete user");
            }
            catch (Exception ex)
            {
                await _logger.Error("Error deleting user");
                return StatusCode(500, "Server error");
            }
        }

        public async Task<IActionResult> OnPostChangePassword([FromBody] PasswordChangeRequest data)
        {
            try
            {
                var sessionData = HttpContext.Session.GetString("UserSession");

                //if not logged in return
                if (sessionData == null)
                {
                    return RedirectToPage("/Index");
                }

                CurrentUser = JsonConvert.DeserializeObject<UserSession>(sessionData);


                if (CurrentUser == null || !CurrentUser.IsAdmin)
                {
                    return RedirectToPage("/Index");
                }

                if (!Regex.IsMatch(data.NewPassword, "[A-Z]"))
                    return StatusCode(400, "Password must contain at least one uppercase letter.");
                if (!Regex.IsMatch(data.NewPassword, "[a-z]"))
                    return StatusCode(400, "Password must contain at least one lowercase letter.");
                if (!Regex.IsMatch(data.NewPassword, "[0-9]"))
                    return StatusCode(400, "Password must contain at least one number.");
                if (data.NewPassword.Length < 8)
                    return StatusCode(400, "Password must be at least 8 characters long.");
                if (data.NewPassword.Length > 24)
                    return StatusCode(400, "Password must be less than 24 characters long.");

                var result = await UserRepository.UpdateUserPassword(data.UserId, data.NewPassword);

                if (result)
                    return new JsonResult(new { success = true });
                else
                    return StatusCode(500, "Failed to update Password");
            }
            catch (Exception ex)
            {
                await _logger.Error("Error Updateing Password for user");
                return StatusCode(500, "Server error");
            }
        }

        public async Task<IActionResult> OnPostUpdateUser([FromBody] EditUserRequest data)
        {
            try
            {
                var sessionData = HttpContext.Session.GetString("UserSession");
                //if not logged in return
                if (sessionData == null)
                {
                    return RedirectToPage("/Index");
                }
                CurrentUser = JsonConvert.DeserializeObject<UserSession>(sessionData);

                if (CurrentUser == null || !CurrentUser.IsAdmin)
                {
                    return RedirectToPage("/Index");
                }

                User user = await _userRepository.GetUserByIdAsync(data.UserId);
                if (user == null)
                {
                    return NotFound("User not found");
                }

                UserRole userRole = await _userRepository.GetUserRoleByIdAsync(data.UserId);
                if (userRole == null)
                {
                    return NotFound("User role not found");
                }

                if (user.Name != data.Name && !string.IsNullOrWhiteSpace(data.Name))
                {
                    if (data.Name.Length < 3 || data.Name.Length > 25)
                    {
                        return StatusCode(400, "Name must be between 3 and 25 characters long.");
                    }
                    user.Name = data.Name;
                }

                if (user.Email != data.Email && !string.IsNullOrWhiteSpace(data.Email))
                {
                    if (!Regex.IsMatch(data.Email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                    {
                        return StatusCode(400, "Invalid email format.");
                    }
                    user.Email = data.Email;
                }

                if (user.Notes != data.Notes && !string.IsNullOrWhiteSpace(data.Notes))
                {
                    if (data.Notes.Length > 500)
                    {
                        return StatusCode(400, "Notes must be less than 500 characters long.");
                    }
                    user.Notes = data.Notes;
                }

                if (user.Username != data.Username && !string.IsNullOrWhiteSpace(data.Username))
                {
                    if (data.Username.Length < 3 || data.Username.Length > 25)
                    {
                        return StatusCode(400, "Username must be between 3 and 25 characters long.");
                    }
                    user.Username = data.Username;
                }

                if (data.Role != null && !string.IsNullOrWhiteSpace(data.Role))
                {
                    if (!Regex.IsMatch(data.Role, "^(Admin|User|Viewer)$", RegexOptions.IgnoreCase))
                    {
                        return StatusCode(400, "Invalid role. Must be Admin, User, or Viewer.");
                    }
                    userRole.Role = data.Role;
                    userRole.IsAdmin = data.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase);
                    userRole.IsEnabled = data.IsEnabled;
                }

                bool success = false;
                success = await _userRepository.UpdateUserAsync(user);
                success = await _userRepository.UpdateUserRoleAsync(userRole);
                await _logger.Info("Error updating user");
                return new JsonResult(new { success = success });
            }
            catch (Exception ex)
            {
                await _logger.Error("Error updating user");
                return StatusCode(500, "Server error");
            }

        }

        public async Task<IActionResult> OnPostUpdateDbSettingAsync([FromBody] UpdateDbRequest dbRequest)
        {

            var sessionData = HttpContext.Session.GetString("UserSession");

            //if not logged in return
            if (sessionData == null)
            {
                return RedirectToPage("/Index");
            }

            CurrentUser = JsonConvert.DeserializeObject<UserSession>(sessionData);


            if (CurrentUser == null || !CurrentUser.Role.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToPage("/Index");
            }

            if (string.IsNullOrWhiteSpace(dbRequest.DbPath))
            {
                ModelState.AddModelError("", "Database Path can't be empty");
                return Page();
            }


            StoredConfig config = new StoredConfig
            {

                DatabasePath = dbRequest.DbPath.Trim(),

            };

            HttpContext.Session.Remove("UserSession");
            HttpContext.Session.Clear();

            await ConfigureService.UpdateConfigFile(config);

            return new JsonResult(new { success = true, redirect = "/Settings#device" });

        }

        public async Task<IActionResult> OnPostUpdateServerSettingAsync([FromBody] UpdateServerRequest serverRequest)
        {

            var sessionData = HttpContext.Session.GetString("UserSession");

            //if not logged in return
            if (sessionData == null)
            {
                return RedirectToPage("/Index");
            }

            CurrentUser = JsonConvert.DeserializeObject<UserSession>(sessionData);


            if (CurrentUser == null || !CurrentUser.Role.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToPage("/Index");
            }

            if (string.IsNullOrWhiteSpace(serverRequest.ServerUrl) || serverRequest.ServerPort == 0)
            {
                ModelState.AddModelError("", "Server Url and port cant be empty");
                return Page();
            }


            StoredConfig config = new StoredConfig
            {

                ServerURL = serverRequest.ServerUrl,
                ServerPort = serverRequest.ServerPort,


            };

            HttpContext.Session.Remove("UserSession");
            HttpContext.Session.Clear();

            await ConfigureService.UpdateConfigFile(config);

            return new JsonResult(new { success = true, redirect = $"http://{config.ServerURL}:{config.ServerPort}/index" });
        }

        public async Task<IActionResult> OnPostResetServerToFactoryAsync()
        {
            var sessionData = HttpContext.Session.GetString("UserSession");

            if (sessionData == null)
            {
                return RedirectToPage("/Index");
            }
            CurrentUser = JsonConvert.DeserializeObject<UserSession>(sessionData);

            if (CurrentUser == null || !CurrentUser.Role.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToPage("/Index");
            }

            try
            {
               

                await ConfigureService.ResetToFactory();

                HttpContext.Session.Remove("UserSession");
                HttpContext.Session.Clear();
                return new JsonResult(new { success = true, redirect = $"http://127.0.0.1:7171/Index" });
            }
            catch (Exception ex)
            {
                await _logger.Error($"Error copying default config file: {ex.Message}");
                return StatusCode(500, "Failed to reset server. Please try again later.");
            }


        }

        public async Task<IActionResult> OnPostRestartServerAsync()
        {

            var sessionData = HttpContext.Session.GetString("UserSession");

            if (sessionData == null)
            {
                return RedirectToPage("/Index");
            }
            CurrentUser = JsonConvert.DeserializeObject<UserSession>(sessionData);

            if (CurrentUser == null || !CurrentUser.Role.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToPage("/Index");
            }

            try
            {
                HttpContext.Session.Remove("UserSession");
                HttpContext.Session.Clear();

                await ConfigureService.RestartServer();
                return new JsonResult(new { success = true, redirect = "/Index" });
            }
            catch (Exception ex)
            {
                await _logger.Error($"Error Restarting Server: {ex.Message}");
                return StatusCode(500, "Failed to reset server. Please try again later.");
            }

        }

        public async Task<IActionResult> OnPostShowEditProviderModal([FromBody] string providerId)
        {
            // Fetch the DNSProvider from your repo
            DNSProvider provider = await _dnsProviderRepository.GetDNSProviderById(providerId);

            if (provider == null)
                return Content("<div class='text-danger'>Provider not found.</div>", "text/html");

            string optionsHtml = string.Join("", SupportedAutoProviders.Select(p =>
                $"<option value='{p}'{(p == provider.Provider ? " selected" : "")}>{p}</option>"));

            var html = $@"
            <form id='editProviderForm' class='p-4 bg-white rounded shadow-sm' style='max-width: 500px; margin: auto;'>
                <h4 class='mb-3 text-center text-primary fw-bold'>Edit DNS Provider</h4>
                <input type='hidden' id='editProviderId' value='{provider.ProviderId}'>
                <div class='mb-3'>
                    <label for='editProviderName' class='form-label'>Name</label>
                    <input type='text' id='editProviderName' class='form-control' value='{provider.ProviderName}' required>
                </div>
                <div class='mb-3'>
                    <label for='editProvider' class='form-label'>Provider</label>
                    <select id='editProvider' class='form-select' required>
                        <option disabled value=''>-- Select a Provider --</option>
                        {optionsHtml}
                    </select>
                </div>
                <div class='mb-3'>
                    <label for='editApiKey' class='form-label'>API Key</label>
                    <input type='text' id='editApiKey' class='form-control' value='{provider.APIKey}' required>
                </div>
                <div class='mb-3'>
                    <label for='editTtl' class='form-label'>TTL (Time to Live)</label>
                    <input type='number' id='editTtl' class='form-control' value='{provider.Ttl}' min='60'>
                </div>
                <div class='d-flex justify-content-end gap-2'>
                    <button type='button' class='btn btn-danger' onclick='promptDeleteProvider(""{provider.ProviderId}"")'>Delete</button>
                    <button type='button' class='btn btn-primary' onclick='saveEditedProvider()'>Save</button>
                    <button type='button' class='btn btn-secondary' data-bs-dismiss='modal'>Close</button>
                </div>
            </form>";
            return Content(html, "text/html");

        }

        public async Task<IActionResult> OnPostDeleteDNSProviderAsync([FromBody] ProviderDeleteRequest request)
        {
            var providerId = request.ProviderId;
            var sessionData = HttpContext.Session.GetString("UserSession");

            if (string.IsNullOrEmpty(sessionData))
            {

                return new JsonResult(new { success = false, redirect = "/Index" });
            }
            CurrentUser = JsonConvert.DeserializeObject<UserSession>(sessionData);
            if (CurrentUser == null)
            {
                return new JsonResult(new { success = false, redirect = "/Index" });
            }

            try
            {

                bool success = await _dnsProviderRepository.DeleteDNSProviderById(providerId);
                return new JsonResult(new { success = success });
            }
            catch (Exception ex)
            {
                await _logger.Error($"Error Deleting DNS Provider: {ex.Message}");
                return new JsonResult(new { success = false, message = "Internal error occurred." });
            }
        }

        public async Task<IActionResult> OnPostUpdateDNSProviderAsync([FromBody] UpdateDNSProviderRequest provider)
        {

            var sessionData = HttpContext.Session.GetString("UserSession");
            if (string.IsNullOrEmpty(sessionData))
            {

                return new JsonResult(new { success = false, redirect = "/Index" });
            }
            CurrentUser = JsonConvert.DeserializeObject<UserSession>(sessionData);
            if (CurrentUser == null)
            {
                return new JsonResult(new { success = false, redirect = "/Index" });
            }
            DNSProvider existingProvider = await _dnsProviderRepository.GetDNSProviderById(provider.ProviderId);

            if (existingProvider.UserId != CurrentUser.UserId && !CurrentUser.IsAdmin)
            {
                return new JsonResult(new { success = false, message = "You do not own this DNS provider." });
            }


            try
            {

                if (existingProvider == null)
                {
                    return new JsonResult(new { success = false, message = "Provider not found." });
                }

                if (!string.IsNullOrWhiteSpace(provider.ProviderName) && existingProvider.ProviderName != provider.ProviderName)
                {
                    existingProvider.ProviderName = provider.ProviderName;
                }

                if (!string.IsNullOrWhiteSpace(provider.Provider) && existingProvider.Provider != provider.Provider)
                {
                    existingProvider.Provider = provider.Provider;
                }

                if (!string.IsNullOrWhiteSpace(provider.APIKey) && existingProvider.APIKey != provider.APIKey)
                {
                    existingProvider.APIKey = provider.APIKey;
                }

                if (provider.Ttl > 0 && existingProvider.Ttl != provider.Ttl)
                {
                    existingProvider.Ttl = provider.Ttl;
                }

                if (existingProvider.Username != CurrentUser.Username && existingProvider.UserId == CurrentUser.UserId)
                {
                    existingProvider.Username = CurrentUser.Username;
                }

                bool success = await _dnsProviderRepository.UpdateDNSProvider(existingProvider);
                return new JsonResult(new { success = success });
            }
            catch (Exception ex)
            {
                await _logger.Error($"Error Restarting Server: {ex.Message}");
                return new JsonResult(new { success = false, message = "Internal error occurred." });
            }
        }

        public async Task<IActionResult> OnPostUpdateCAUrlAsync([FromBody] CAUpdateRequest caUrlRequest)
        {
            var sessionData = HttpContext.Session.GetString("UserSession");
            if (sessionData == null)
                return RedirectToPage("/Index");

            CurrentUser = JsonConvert.DeserializeObject<UserSession>(sessionData);
            if (CurrentUser == null || !CurrentUser.Role.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase))
                return RedirectToPage("/Index");



            StoredConfig config = new StoredConfig
            {

                CAPrimeUrl = caUrlRequest.CAPrimeUrl,
                CAStagingUrl = caUrlRequest.CAStagingUrl,

            };

            HttpContext.Session.Remove("UserSession");
            HttpContext.Session.Clear();

            await ConfigureService.UpdateConfigFile(config);

            return new JsonResult(new { success = true, redirect = "/Settings#advanced" });
        }

        public async Task<IActionResult> OnPostUpdateCertSettingsAsync([FromBody] CertSettingsRequest request)
        {
            var sessionData = HttpContext.Session.GetString("UserSession");
            if (sessionData == null)
                return RedirectToPage("/Index");

            CurrentUser = JsonConvert.DeserializeObject<UserSession>(sessionData);
            if (CurrentUser == null || !CurrentUser.Role.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase))
                return RedirectToPage("/Index");

            StoredConfig config = new StoredConfig
            {
                RenewBeforeExpiryDays = request.RenewBeforeExpiryDays,
                CertValidityDays = request.CertValidityDays
            };

            await ConfigureService.UpdateConfigFile(config);

            return new JsonResult(new { success = true });
        }

        public async Task<IActionResult> OnPostUpdateModeSettingsAsync([FromBody] ModeSettingsRequest req)
        {
            var sessionData = HttpContext.Session.GetString("UserSession");
            if (sessionData == null) return new JsonResult(new { success = false, message = "Not logged in." });
            CurrentUser = JsonConvert.DeserializeObject<UserSession>(sessionData);
            if (CurrentUser == null || !CurrentUser.Role.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase))
                return new JsonResult(new { success = false, message = "Not authorized." });

            StoredConfig config = new StoredConfig
            {
                StagingOnly = req.StagingOnly,
                RestrictViewers = req.RestrictViewers,
                HideViewerLogout = req.HideViewerLogout,
                DemoLoginEnabled = req.DemoLoginEnabled,
                DemoUsername = req.DemoUsername,
                DemoPassword = req.DemoPassword
            };
            await ConfigureService.UpdateConfigFile(config);
            return new JsonResult(new { success = true, message = "Mode settings saved." });
        }

        public async Task<IActionResult> OnPostAddConnectionAsync([FromBody] ConnectionRequest req)
        {
            var sessionData = HttpContext.Session.GetString("UserSession");
            if (sessionData == null) return new JsonResult(new { success = false, message = "Not logged in." });
            CurrentUser = JsonConvert.DeserializeObject<UserSession>(sessionData);
            if (CurrentUser == null) return new JsonResult(new { success = false, message = "Not logged in." });

            var conn = new UserConnection
            {
                ConnectionId   = Guid.NewGuid().ToString("N"),
                UserId         = CurrentUser.UserId,
                ConnectionName = req.ConnectionName,
                ConnectionType = req.ConnectionType,
                IsEnabled      = req.IsEnabled,
                Settings       = req.Settings.ToString(),
                OnPreRenew     = req.OnPreRenew,
                OnPreExpiry    = req.OnPreExpiry,
                OnRenewSuccess = req.OnRenewSuccess,
                OnRenewFail    = req.OnRenewFail,
                CreatedAt      = DateTime.UtcNow
            };
            bool ok = await _connectionRepository.InsertConnectionAsync(conn);
            return new JsonResult(new { success = ok, message = ok ? "Connection added." : "Failed to add connection.", connectionId = conn.ConnectionId });
        }

        public async Task<IActionResult> OnPostUpdateConnectionAsync([FromBody] ConnectionRequest req)
        {
            var sessionData = HttpContext.Session.GetString("UserSession");
            if (sessionData == null) return new JsonResult(new { success = false, message = "Not logged in." });
            CurrentUser = JsonConvert.DeserializeObject<UserSession>(sessionData);
            if (CurrentUser == null) return new JsonResult(new { success = false, message = "Not logged in." });

            var conn = new UserConnection
            {
                ConnectionId   = req.ConnectionId,
                UserId         = CurrentUser.UserId,
                ConnectionName = req.ConnectionName,
                ConnectionType = req.ConnectionType,
                IsEnabled      = req.IsEnabled,
                Settings       = req.Settings.ToString(),
                OnPreRenew     = req.OnPreRenew,
                OnPreExpiry    = req.OnPreExpiry,
                OnRenewSuccess = req.OnRenewSuccess,
                OnRenewFail    = req.OnRenewFail,
                CreatedAt      = DateTime.UtcNow
            };
            bool ok = await _connectionRepository.UpdateConnectionAsync(conn);
            return new JsonResult(new { success = ok, message = ok ? "Connection updated." : "Failed to update connection." });
        }

        public async Task<IActionResult> OnGetGetConnectionAsync(string connectionId)
        {
            var sessionData = HttpContext.Session.GetString("UserSession");
            if (sessionData == null) return new JsonResult(new { success = false });
            CurrentUser = JsonConvert.DeserializeObject<UserSession>(sessionData);
            if (CurrentUser == null) return new JsonResult(new { success = false });

            var conn = await _connectionRepository.GetConnectionByIdAsync(connectionId);
            if (conn == null || conn.UserId != CurrentUser.UserId)
                return new JsonResult(new { success = false });

            return new JsonResult(new { success = true, connection = conn });
        }

        public async Task<IActionResult> OnPostDeleteConnectionAsync([FromBody] ConnectionRequest req)
        {
            var sessionData = HttpContext.Session.GetString("UserSession");
            if (sessionData == null) return new JsonResult(new { success = false, message = "Not logged in." });
            CurrentUser = JsonConvert.DeserializeObject<UserSession>(sessionData);
            if (CurrentUser == null) return new JsonResult(new { success = false, message = "Not logged in." });

            bool ok = await _connectionRepository.DeleteConnectionAsync(req.ConnectionId, CurrentUser.UserId);
            return new JsonResult(new { success = ok, message = ok ? "Connection deleted." : "Failed to delete." });
        }

        public async Task<IActionResult> OnPostTestConnectionAsync([FromBody] ConnectionRequest req)
        {
            var sessionData = HttpContext.Session.GetString("UserSession");
            if (sessionData == null) return new JsonResult(new { success = false, message = "Not logged in." });
            CurrentUser = JsonConvert.DeserializeObject<UserSession>(sessionData);
            if (CurrentUser == null) return new JsonResult(new { success = false, message = "Not logged in." });

            var conn = new UserConnection
            {
                ConnectionId   = "test",
                UserId         = CurrentUser.UserId,
                ConnectionName = req.ConnectionName,
                ConnectionType = req.ConnectionType,
                IsEnabled      = true,
                Settings       = req.Settings.ToString(),
                OnPreRenew = true, OnPreExpiry = true, OnRenewSuccess = true, OnRenewFail = true,
                CreatedAt      = DateTime.UtcNow
            };
            try
            {
                await NotificationService.SendTestAsync(conn, "SphereSSL test notification — connection is working!");
                return new JsonResult(new { success = true, message = "Test notification sent." });
            }
            catch (Exception ex)
            {
                var detail = ex.InnerException?.Message ?? ex.Message;
                return new JsonResult(new { success = false, message = $"Test failed: {detail}" });
            }
        }
    }
}
