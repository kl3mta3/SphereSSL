using Microsoft.AspNetCore.Mvc;
using SphereSSLv2.Data;
using SphereSSLv2.Services;
using System.Diagnostics;
namespace SphereSSLv2.Controllers
{
    [Route("Server/[action]")]
    public class ServerController : Controller
    {
        [HttpPost]
        public IActionResult Restart()
        {
            if (!Spheressl.UseLogOn || Spheressl.IsLogIn)
            {
                try
                {
                    var exePath = Environment.ProcessPath;
                    Logger.Debug($"[RESTART] Relaunching: {exePath}");

                    Process.Start(exePath);           // Relaunch the app
                    Environment.Exit(0);              // Kill this one

                    return Ok("Restart triggered.");
                }
                catch (Exception ex)
                {
                    Logger.Debug($"[RESTART ERROR] {ex.Message}");
                    return StatusCode(500, $"Restart failed: {ex.Message}");
                }
            }

            return Unauthorized("Not logged in.");
        }

        [HttpGet("/select-folder")]
        public async Task<string> GetFolderPath()
        {
            Console.WriteLine("[WEB] Requesting folder from tray app...");

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


        [HttpPost("/shutdown")]
        public IActionResult Shutdown()
        {
            Console.WriteLine($"Shutdown request received");
            Logger.Info("Shutting down...");
            Task.Run(() => Environment.Exit(0));
            return Ok("Shutting down.");
        }
    }
}
