using SphereSSLv2.Services.Config;

namespace SphereSSLv2.Services.APISupportedProviders
{
    public class DuckDNSHelper
    {
        private const string BaseUrl = "https://www.duckdns.org/update";

        /// <summary>
        /// Adds or replaces the _acme-challenge TXT record for a DuckDNS subdomain.
        /// </summary>
        /// <param name="_logger">Logger instance for status output.</param>
        /// <param name="domain">The DuckDNS subdomain (e.g. 'yourname' for 'yourname.duckdns.org').</param>
        /// <param name="apiToken">The DuckDNS token for the user (not a secret/key pair).</param>
        /// <param name="content">TXT record content (ACME challenge value).</param>
        /// <param name="username">Username for logging.</param>
        /// <returns>The domain name if successful, or null if failed.</returns>
        internal static async Task<string> AddDNSRecord(Logger _logger, string domain, string apiToken, string content, string username)
        {

            var url = $"{BaseUrl}?domains={Uri.EscapeDataString(domain)}&token={apiToken}&txt={Uri.EscapeDataString(content)}&clear=false";

            using var client = new HttpClient();
            var response = await client.GetAsync(url);
            var responseText = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode && responseText.Trim().Equals("OK", StringComparison.OrdinalIgnoreCase))
            {
                await _logger.Info($"[{username}]: DuckDNS TXT record set successfully.");
                return $"{domain}.duckdns.org";
            }
            else
            {
                await _logger.Debug($"[{username}]: Failed to set DuckDNS TXT record:\n{response.StatusCode}\n{responseText}");
                return null;
            }
        }

        /// <summary>
        /// Deletes the _acme-challenge TXT record for a DuckDNS subdomain (by setting it empty).
        /// </summary>
        internal static async Task<bool> DeleteAllAcmeChallengeRecords(Logger _logger, string domain, string apiToken, string username)
        {
            var url = $"{BaseUrl}?domains={Uri.EscapeDataString(domain)}&token={apiToken}&txt=&clear=true";
            using var client = new HttpClient();
            var response = await client.GetAsync(url);
            var responseText = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode && responseText.Trim().Equals("OK", StringComparison.OrdinalIgnoreCase))
            {
                await _logger.Info($"[{username}]: DuckDNS TXT record cleared.");
                return true;
            }
            else
            {
                await _logger.Debug($"[{username}]: Failed to clear DuckDNS TXT record:\n{response.StatusCode}\n{responseText}");
                return false;
            }
        }
    }
}
