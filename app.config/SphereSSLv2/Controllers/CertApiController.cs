using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using SphereSSLv2.Data.Repositories;
using SphereSSLv2.Models.CertModels;
using SphereSSLv2.Services.Config;

namespace SphereSSLv2.Controllers
{
    [ApiController]
    [Route("api/cert")]
    public class CertApiController : ControllerBase
    {
        private readonly Logger _logger;

        public CertApiController(Logger logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// GET /api/cert/{domain}
        /// Returns the cert PEM and private key for the given domain.
        /// Requires header: X-Api-Key: {cert-specific key shown in Manage → Request API}
        /// </summary>
        [HttpGet("{domain}")]
        public async Task<IActionResult> GetCert(string domain)
        {
            if (!Request.Headers.TryGetValue("X-Api-Key", out var apiKey) || string.IsNullOrWhiteSpace(apiKey))
                return Unauthorized(new { error = "Missing X-Api-Key header." });

            // Look up cert directly by domain
            string orderId = await GetOrderIdByDomainAsync(domain);
            if (orderId == null)
                return NotFound(new { error = $"No certificate found for domain '{domain}'." });

            CertRecord cert = await CertRepository.GetCertRecordByOrderId(orderId);
            if (cert == null)
                return NotFound(new { error = $"Certificate record not found for domain '{domain}'." });

            // Validate the cert-specific API key
            if (string.IsNullOrEmpty(cert.CertApiKey) || cert.CertApiKey != apiKey.ToString())
                return Unauthorized(new { error = "Invalid API key for this domain." });

            if (string.IsNullOrEmpty(cert.CertPem) || string.IsNullOrEmpty(cert.CertKey))
                return NotFound(new { error = "Certificate content not stored. Re-issue the certificate to enable downloads." });

            _ = _logger.Info($"[API] Cert retrieved for domain '{domain}'");

            return Ok(new
            {
                domain,
                certPem = cert.CertPem,
                certKey = cert.CertKey,
                expiryDate = cert.ExpiryDate.ToString("O"),
                orderId = cert.OrderId
            });
        }

        private static async Task<string?> GetOrderIdByDomainAsync(string domain)
        {
            using var conn = new SqliteConnection($"Data Source={ConfigureService.dbPath}");
            await conn.OpenAsync();

            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT c.OrderId FROM Challenges c
                INNER JOIN CertRecords cr ON cr.OrderId = c.OrderId
                WHERE c.Domain = @domain OR c.Domain = @wildcard
                ORDER BY cr.CreationTime DESC
                LIMIT 1";
            cmd.Parameters.AddWithValue("@domain", domain);
            int dot = domain.IndexOf('.');
            cmd.Parameters.AddWithValue("@wildcard", dot >= 0 ? "*" + domain.Substring(dot) : domain);

            var result = await cmd.ExecuteScalarAsync();
            return result as string;
        }
    }
}
