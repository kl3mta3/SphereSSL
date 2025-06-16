using Microsoft.AspNetCore.Mvc;
using SphereSSLv2.Data;
using SphereSSLv2.Services;
using System.Diagnostics;
using System.Threading.Tasks;
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

        [HttpPost]
        public IActionResult Restart()
        {
            if (!Spheressl.UseLogOn || Spheressl.IsLogIn)
            {
                 
                try
                {
                    var exePath = Environment.ProcessPath;
                    _ = _logger.Debug($"[RESTART] Relaunching: {exePath}");

                    Process.Start(exePath);           // Relaunch the app
                    Environment.Exit(0);              // Kill this one

                    return Ok("Restart triggered.");
                }
                catch (Exception ex)
                {
                    _ = _logger.Debug($"[RESTART ERROR] {ex.Message}");
                    return StatusCode(500, $"Restart failed: {ex.Message}");
                }
            }

            return Unauthorized("Not logged in.");
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


        [HttpPost("/shutdown")]
        public async Task<IActionResult> Shutdown()
        {
          
            await _logger.Info($"Shutdown request received");
            await  _logger.Info("Shutting down...");
            await Task.Run(() => Environment.Exit(0));
            return Ok("Shutting down.");
        }
    }
}
