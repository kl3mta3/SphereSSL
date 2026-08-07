using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using SphereSSLv2.Models.Dtos;
using SphereSSLv2.Models.UserModels;
using SphereSSLv2.Services.Config;
using System.Net;
using System.Text;
using System.Text.Json;

namespace SphereSSLv2.Controllers
{
    [Route("Server/[action]")]
    public class ServerController : Controller
    {
        private readonly Logger _logger;

        public ServerController(Logger logger)
        {
            _logger = logger;
        }

        [HttpGet("/restart")]
        public async Task<IActionResult> Restart()
        {
            if (!IsSuperAdmin())
                return Unauthorized();

            using var client = new HttpClient();
            try
            {
                var results = await client.GetStringAsync("http://localhost:7172/restart/");
                return Content(results);
            }
            catch (Exception ex)
            {
                _ = _logger.Error($"[RESTART ERROR] {ex.Message}");
                return StatusCode(502, "Restart service is unavailable.");
            }
        }

        [HttpGet("/factory-reset")]
        public async Task<IActionResult> FactoryReset()
        {
            if (!IsSuperAdmin())
                return Unauthorized();

            using var client = new HttpClient();
            try
            {
                var results = await client.GetStringAsync("http://localhost:7172/factory-reset/");
                return Content(results);
            }
            catch (Exception ex)
            {
                _ = _logger.Error($"[FACTORY RESET ERROR] {ex.Message}");
                return StatusCode(502, "Factory reset service is unavailable.");
            }
        }

        [HttpGet("/select-folder")]
        public async Task<IActionResult> GetFolderPath()
        {
            if (GetSessionUser() == null)
                return Unauthorized();

            using var client = new HttpClient();
            try
            {
                var result = await client.GetStringAsync("http://localhost:7172/select-folder/");
                return Content(result);
            }
            catch (Exception ex)
            {
                _ = _logger.Error($"[SELECT FOLDER ERROR] {ex.Message}");
                return StatusCode(502, "Folder selection service is unavailable.");
            }
        }

        [HttpGet("/open-location")]
        public async Task<IActionResult> OpenFolderPath([FromQuery] string path)
        {
            if (GetSessionUser() == null)
                return Unauthorized();

            using var client = new HttpClient();
            try
            {
                await client.GetStringAsync($"http://localhost:7172/open-location/?path={WebUtility.UrlEncode(path)}");
                return NoContent();
            }
            catch (Exception ex)
            {
                _ = _logger.Error($"[OPEN LOCATION ERROR] {ex.Message}");
                return StatusCode(502, "Open-location service is unavailable.");
            }
        }

        [HttpPost("/update-db-path")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateDBPath(string path)
        {
            if (!IsSuperAdmin())
                return Unauthorized();

            using var client = new HttpClient();
            try
            {
                var results = await client.GetStringAsync($"http://localhost:7172/update-db-path/?path={WebUtility.UrlEncode(path)}");
                return Content(results);
            }
            catch (Exception ex)
            {
                _ = _logger.Error($"[UPDATE DB PATH ERROR] {ex.Message}");
                return StatusCode(502, "Database path service is unavailable.");
            }
        }

        [HttpPost("/update-url-path")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateServerPath(UpdateServerRequest request)
        {
            if (!IsSuperAdmin())
                return Unauthorized();

            using var client = new HttpClient();
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await client.PostAsync("http://localhost:7172/update-url-path/", content);
                var responseBody = await response.Content.ReadAsStringAsync();
                return StatusCode((int)response.StatusCode, responseBody);
            }
            catch (Exception ex)
            {
                _ = _logger.Error($"[UPDATE SERVER PATH ERROR] {ex.Message}");
                return StatusCode(502, "Server path service is unavailable.");
            }
        }

        private UserSession? GetSessionUser()
        {
            var sessionData = HttpContext.Session.GetString("UserSession");
            if (string.IsNullOrWhiteSpace(sessionData))
                return null;

            try
            {
                return JsonConvert.DeserializeObject<UserSession>(sessionData);
            }
            catch (Newtonsoft.Json.JsonException)
            {
                return null;
            }
        }

        private bool IsSuperAdmin()
        {
            var currentUser = GetSessionUser();
            return string.Equals(currentUser?.Role, "SuperAdmin", StringComparison.OrdinalIgnoreCase);
        }
    }
}
