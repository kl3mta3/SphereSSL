using SphereSSLv2.Services.Config;
using System.Text.Json;
using System.Text;

namespace SphereSSLv2.Services.APISupportedProviders
{
    public class HetznerDNSHelper
    {
        private const string BaseUrl = "https://dns.hetzner.com/api/v1";

        /// <summary>
        /// Adds a TXT DNS record (_acme-challenge) for the specified domain using Hetzner DNS API.
        /// Requires a Hetzner DNS API Token (single token string).
        /// </summary>
        /// <param name="_logger">Logger for debug/info output.</param>
        /// <param name="domain">FQDN to update (e.g., example.com).</param>
        /// <param name="apiToken">Hetzner API Token (looks like: <c>q0VJZk0d...</c>).</param>
        /// <param name="content">TXT value to add (the ACME challenge).</param>
        /// <param name="username">Username for logging (not required by API).</param>
        /// <returns>The DNS zone ID if successful, null otherwise.</returns>
        internal static async Task<string> AddDNSRecord(Logger _logger, string domain, string apiToken, string content, string username)
        {
            // Get zone ID first
            string zoneId = await GetZoneId(_logger, domain, apiToken, username);
            if (string.IsNullOrEmpty(zoneId))
            {
                await _logger.Debug("Failed to retrieve zone ID for the domain.");
                return null;
            }

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Auth-API-Token", apiToken);

            var requestBody = new
            {
                zone_id = zoneId,
                type = "TXT",
                name = $"_acme-challenge.{domain.TrimEnd('.')}",
                value = content,
                ttl = 120
            };

            var json = JsonSerializer.Serialize(requestBody);
            var contentBody = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync($"{BaseUrl}/records", contentBody);
            var responseText = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                await _logger.Info("DNS record added successfully.");
                return zoneId;
            }
            else
            {
                await _logger.Debug($"[{username}]: Failed to add DNS record:\n{response.StatusCode}\n{responseText}");
                return null;
            }
        }

        /// <summary>
        /// Gets the Hetzner DNS zone ID for a given domain.
        /// </summary>
        internal static async Task<string> GetZoneId(Logger _logger, string domain, string apiToken, string username)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Auth-API-Token", apiToken);

            var response = await client.GetAsync($"{BaseUrl}/zones");
            var responseText = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                await _logger.Debug($"[{username}]: Failed to fetch zones: {response.StatusCode}\n{responseText}");
                return null;
            }

            try
            {
                using var doc = JsonDocument.Parse(responseText);
                var zones = doc.RootElement.GetProperty("zones");
                foreach (var zone in zones.EnumerateArray())
                {
                    var zoneName = zone.GetProperty("name").GetString();
                    if (zoneName.Equals(domain, StringComparison.OrdinalIgnoreCase) || domain.EndsWith("." + zoneName))
                    {
                        return zone.GetProperty("id").GetString();
                    }
                }
            }
            catch (Exception ex)
            {
                await _logger.Debug($"[{username}]: Exception parsing zones: {ex.Message}");
                return null;
            }

            await _logger.Debug($"[{username}]: Domain not found in Hetzner account.");
            return null;
        }

        /// <summary>
        /// Deletes all _acme-challenge TXT records for a given domain in Hetzner DNS.
        /// </summary>
        /// <param name="_logger">Logger for info/debug output</param>
        /// <param name="domain">Domain to search (e.g. mysite.com)</param>
        /// <param name="apiToken">Hetzner DNS API token</param>
        /// <param name="username">For logging only</param>
        /// <returns>True if all matching records were deleted (or none exist), false otherwise.</returns>
        internal static async Task<bool> DeleteAllAcmeChallengeRecords(Logger _logger, string domain, string apiToken, string username)
        {
            string zoneId = await GetZoneId(_logger, domain, apiToken, username);
            if (string.IsNullOrEmpty(zoneId))
            {
                await _logger.Debug("Failed to retrieve zone ID for the domain.");
                return false;
            }

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Auth-API-Token", apiToken);

            // Get all records in the zone
            var resp = await client.GetAsync($"{BaseUrl}/records?zone_id={zoneId}");
            var respText = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                await _logger.Debug($"[{username}]: Failed to fetch DNS records: {resp.StatusCode}\n{respText}");
                return false;
            }

            bool allSuccess = true;
            try
            {
                using var doc = JsonDocument.Parse(respText);
                var records = doc.RootElement.GetProperty("records");
                foreach (var rec in records.EnumerateArray())
                {
                    string type = rec.GetProperty("type").GetString();
                    string name = rec.GetProperty("name").GetString();
                    string recId = rec.GetProperty("id").GetString();

                    if (type == "TXT" && name.StartsWith("_acme-challenge", StringComparison.OrdinalIgnoreCase))
                    {
                        var delResp = await client.DeleteAsync($"{BaseUrl}/records/{recId}");
                        var delTxt = await delResp.Content.ReadAsStringAsync();
                        if (delResp.IsSuccessStatusCode)
                            await _logger.Info($"[{username}]: Deleted TXT record ID {recId} ({name}).");
                        else
                        {
                            await _logger.Debug($"[{username}]: Failed to delete TXT record ID {recId}: {delResp.StatusCode}\n{delTxt}");
                            allSuccess = false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await _logger.Debug($"[{username}]: Exception deleting records: {ex.Message}");
                return false;
            }

            return allSuccess;
        }

    }
}
