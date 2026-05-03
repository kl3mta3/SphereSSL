using SphereSSLv2.Services.Config;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace SphereSSLv2.Services.APISupportedProviders
{
    public class GandiDNSHelper
    {
        private const string BaseUrl = "https://api.gandi.net/v5/livedns";

        /// <summary>
        /// Adds a _acme-challenge TXT record to the specified domain at Gandi.net.
        /// </summary>
        /// <param name="_logger">Logger for logging output.</param>
        /// <param name="domain">The apex domain (zone), e.g. 'example.com'.</param>
        /// <param name="apiToken">Gandi API key (no secret required).</param>
        /// <param name="content">TXT record content.</param>
        /// <param name="username">Username for logging.</param>
        /// <returns>The zone name if successful, null if failed.</returns>
        internal static async Task<string> AddDNSRecord(Logger _logger, string domain, string apiToken, string content, string username)
        {
            // Gandi requires a full zone, like example.com, not a subdomain.
            // Record name must be _acme-challenge[.subdomain]
            string recordName = $"_acme-challenge";
            var url = $"{BaseUrl}/domains/{domain}/records/{recordName}/TXT";

            var requestBody = new
            {
                rrset_ttl = 300,
                rrset_values = new[] { content }
            };
            var json = JsonSerializer.Serialize(requestBody);

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Apikey", apiToken);

            var contentBody = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PutAsync(url, contentBody);
            var responseText = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                await _logger.Info($"[{username}]: Gandi TXT record added successfully.");
                return domain;
            }
            else
            {
                await _logger.Debug($"[{username}]: Failed to add Gandi TXT record:\n{response.StatusCode}\n{responseText}");
                return null;
            }
        }

        /// <summary>
        /// Deletes all _acme-challenge TXT records from the specified domain at Gandi.net.
        /// </summary>
        internal static async Task<bool> DeleteAllAcmeChallengeRecords(Logger _logger, string domain, string apiToken, string username)
        {
            string recordName = $"_acme-challenge";
            var url = $"{BaseUrl}/domains/{domain}/records/{recordName}/TXT";

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Apikey", apiToken);

            var response = await client.DeleteAsync(url);
            var responseText = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                await _logger.Info($"[{username}]: Gandi TXT records deleted.");
                return true;
            }
            else
            {
                await _logger.Debug($"[{username}]: Failed to delete Gandi TXT record:\n{response.StatusCode}\n{responseText}");
                return false;
            }
        }
    }
}
