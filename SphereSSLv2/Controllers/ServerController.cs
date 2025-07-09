using Microsoft.AspNetCore.Mvc;
using SphereSSLv2.Models.Dtos;
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
        public async Task<string> Restart()
        {
            using var client = new HttpClient();
            try
            {


                var results = await client.GetStringAsync("http://localhost:7172/restart/");


                return results;
            }
            catch (Exception ex)
            {
                _ = _logger.Error($"[RESTART ERROR] {ex.Message}");

                return $"Restart failed: {ex.Message}";
            }
        }

        [HttpGet("/factory-reset")]
        public async Task<string> FactoryReset()
        {
            using var client = new HttpClient();
            try
            {
                var results = await client.GetStringAsync($"http://localhost:7172/factory-reset/");

                return results;
            }
            catch (Exception ex)
            {
                _ = _logger.Error($"[RESTART ERROR] {ex.Message}");

                return $"Restart failed: {ex.Message}";
            }
        }

        [HttpGet("/select-folder")]
        public async Task<string> GetFolderPath()
        {


            using var client = new HttpClient();

            try
            {
                var result = await client.GetStringAsync("http://localhost:7172/select-folder/");


                return result;
            }
            catch (Exception ex)
            {

                return string.Empty;
            }
        }

        [HttpGet("/open-location")]
        public async Task OpenFolderPath([FromQuery] string path)
        {


            using var client = new HttpClient();

            try
            {
                var result = await client.GetStringAsync($"http://localhost:7172/open-location/?path={WebUtility.UrlEncode(path)}");
                return;
            }
            catch
            {
                return;
            }
        }

        [HttpPost("/update-db-path")]
        public async Task<string> UpdateDBPath(string path)
        {
            using var client = new HttpClient();
            try
            {


                var results = await client.GetStringAsync($"http://localhost:7172/update-db-path/?path={WebUtility.UrlEncode(path)}");

                return results; ;
            }
            catch (Exception ex)
            {
                _ = _logger.Error($"[UPDATE ERROR] {ex.Message}");

                return $"Update failed: {ex.Message}"; 
            }

        }

        [HttpPost("/update-url-path")]
        public async Task<string> UpdateserverPath(UpdateServerRequest request)
        {
            using var client = new HttpClient();
            try
            {
                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await client.PostAsync("http://localhost:7172/update-url-path/", content);

                return await response.Content.ReadAsStringAsync();

            }
            catch (Exception ex)
            {
                _ = _logger.Error($"[UPDATE ERROR] {ex.Message}");

                return $"Update failed: {ex.Message}";
            }

        }

    }
}
