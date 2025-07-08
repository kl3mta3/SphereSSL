using SphereSSLv2.Services.Config;
using System.Text;
using System.Text.Json;

namespace SphereSSLv2.Services.APISupportedProviders
{
    public class GoDaddyDNSHelper
    {
        private const string BaseUrl = "https://api.godaddy.com/v1";

        /// <summary>
        /// Adds a _acme-challenge TXT record to the specified GoDaddy domain.
        /// </summary>
        /// <param name="_logger">Logger for output.</param>
        /// <param name="domain">Apex domain (zone), e.g. 'example.com'.</param>
        /// <param name="apiToken">API_KEY:API_SECRET combo.</param>
        /// <param name="content">TXT record value.</param>
        /// <param name="username">Username for logging.</param>
        /// <returns>The domain name if success, otherwise null.</returns>
        internal static async Task<string> AddDNSRecord(Logger _logger, string domain, string apiToken, string content, string username)
        {
            string[] parts = apiToken.Split(':');
            if (parts.Length != 2)
            {
                await _logger.Error($"[{username}]: GoDaddy API token invalid. Should be API_KEY:API_SECRET");
                return null;
            }
            var apiKey = parts[0];
            var apiSecret = parts[1];

            // The record name should be "_acme-challenge" or "_acme-challenge.sub" for SANs
            string recordName = $"_acme-challenge";

            var url = $"{BaseUrl}/domains/{domain}/records/TXT/{recordName}";

            // GoDaddy expects an *array* of strings (all TXT values for that name)
            var requestBody = new[]
            {
            new { data = content, ttl = 600 }
        };

            var json = JsonSerializer.Serialize(requestBody);

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", $"sso-key {apiKey}:{apiSecret}");

            var contentBody = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PutAsync(url, contentBody);
            var responseText = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                await _logger.Info($"[{username}]: GoDaddy TXT record added successfully.");
                return domain;
            }
            else
            {
                await _logger.Debug($"[{username}]: Failed to add GoDaddy TXT record:\n{response.StatusCode}\n{responseText}");
                return null;
            }
        }

        /// <summary>
        /// Deletes all _acme-challenge TXT records for the domain (by setting empty array).
        /// </summary>
        internal static async Task<bool> DeleteAllAcmeChallengeRecords(Logger _logger, string domain, string apiToken, string username)
        {
            string[] parts = apiToken.Split(':');
            if (parts.Length != 2)
            {
                await _logger.Error($"[{username}]: GoDaddy API token invalid. Should be API_KEY:API_SECRET");
                return false;
            }
            var apiKey = parts[0];
            var apiSecret = parts[1];

            string recordName = $"_acme-challenge";
            var url = $"{BaseUrl}/domains/{domain}/records/TXT/{recordName}";

            // Sending an empty array deletes all records for that name
            var requestBody = Array.Empty<object>();
            var json = JsonSerializer.Serialize(requestBody);

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", $"sso-key {apiKey}:{apiSecret}");

            var contentBody = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PutAsync(url, contentBody);
            var responseText = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                await _logger.Info($"[{username}]: GoDaddy TXT records deleted.");
                return true;
            }
            else
            {
                await _logger.Debug($"[{username}]: Failed to delete GoDaddy TXT record:\n{response.StatusCode}\n{responseText}");
                return false;
            }
        }
    }
}
