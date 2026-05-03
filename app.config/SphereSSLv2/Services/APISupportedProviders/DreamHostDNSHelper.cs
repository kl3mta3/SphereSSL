using SphereSSLv2.Services.Config;
using System.Text.Json;

namespace SphereSSLv2.Services.APISupportedProviders
{
    public class DreamHostDNSHelper
    {
        private const string BaseUrl = "https://api.dreamhost.com/";

        /// <summary>
        /// Adds a TXT record (_acme-challenge) for a domain via DreamHost API.
        /// </summary>
        /// <param name="_logger">Logger instance for status output.</param>
        /// <param name="domain">Domain for which to add the record.</param>
        /// <param name="apiToken">DreamHost API key (string, no secret required).</param>
        /// <param name="content">TXT record content (ACME challenge value).</param>
        /// <param name="username">Username for logging.</param>
        /// <returns>The domain name (DreamHost has no zoneId), or null if failed.</returns>
        internal static async Task<string> AddDNSRecord(Logger _logger, string domain, string apiToken, string content, string username)
        {
            var recordName = $"_acme-challenge.{domain.TrimEnd('.')}";

            var url = $"{BaseUrl}?key={apiToken}&cmd=dns-add_record&format=json" +
                      $"&record={Uri.EscapeDataString(recordName)}" +
                      $"&type=TXT" +
                      $"&value={Uri.EscapeDataString(content)}" +
                      $"&ttl=120";

            using var client = new HttpClient();
            var response = await client.GetAsync(url);
            var responseText = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode && responseText.Contains("\"result\": \"success\""))
            {
                await _logger.Info($"[{username}]: DreamHost DNS record added successfully.");
                return domain;
            }
            else
            {
                await _logger.Debug($"[{username}]: Failed to add DreamHost DNS record:\n{response.StatusCode}\n{responseText}");
                return null;
            }
        }

        /// <summary>
        /// Deletes all _acme-challenge TXT records for the given domain via DreamHost API.
        /// </summary>
        /// <param name="_logger">Logger instance for status output.</param>
        /// <param name="domain">Domain to clean records from.</param>
        /// <param name="apiToken">DreamHost API key (string).</param>
        /// <param name="username">Username for logging.</param>
        /// <returns>True if all deletions succeed, false otherwise.</returns>
        internal static async Task<bool> DeleteAllAcmeChallengeRecords(Logger _logger, string domain, string apiToken, string username)
        {
            var recordName = $"_acme-challenge.{domain.TrimEnd('.')}";

            var listUrl = $"{BaseUrl}?key={apiToken}&cmd=dns-list_records&format=json";
            using var client = new HttpClient();
            var listResp = await client.GetAsync(listUrl);
            var listText = await listResp.Content.ReadAsStringAsync();

            if (!listResp.IsSuccessStatusCode)
            {
                await _logger.Debug($"[{username}]: Failed to fetch DreamHost DNS records:\n{listResp.StatusCode}\n{listText}");
                return false;
            }

            bool allSuccess = true;
            try
            {
                using var doc = JsonDocument.Parse(listText);
                foreach (var record in doc.RootElement.GetProperty("data").EnumerateArray())
                {
                    string recName = record.GetProperty("record").GetString();
                    string recType = record.GetProperty("type").GetString();
                    if (recType == "TXT" && recName.StartsWith("_acme-challenge", StringComparison.OrdinalIgnoreCase))
                    {
                        // 2. Delete this record
                        string value = record.GetProperty("value").GetString();
                        var delUrl = $"{BaseUrl}?key={apiToken}&cmd=dns-remove_record&format=json" +
                                     $"&record={Uri.EscapeDataString(recName)}" +
                                     $"&type=TXT" +
                                     $"&value={Uri.EscapeDataString(value)}";
                        var delResp = await client.GetAsync(delUrl);
                        var delText = await delResp.Content.ReadAsStringAsync();

                        if (delResp.IsSuccessStatusCode && delText.Contains("\"result\": \"success\""))
                            await _logger.Info($"[{username}]: Deleted DreamHost TXT record ({recName}).");
                        else
                        {
                            await _logger.Debug($"[{username}]: Failed to delete DreamHost TXT record:\n{delResp.StatusCode}\n{delText}");
                            allSuccess = false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await _logger.Debug($"[{username}]: Exception deleting DreamHost records: {ex.Message}");
                return false;
            }
            return allSuccess;
        }
    
    }
}
