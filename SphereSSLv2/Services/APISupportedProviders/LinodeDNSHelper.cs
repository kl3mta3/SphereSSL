using SphereSSLv2.Services.Config;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;

namespace SphereSSLv2.Services.APISupportedProviders
{
    public class LinodeDNSHelper
    {
        private const string BaseUrl = "https://api.linode.com/v4";

        /// <summary>
        /// Adds a TXT record for ACME challenge to Linode DNS.
        /// </summary>
        internal static async Task<string> AddDNSRecord(Logger _logger, string domain, string apiToken, string content, string username)
        {
            string zoneId = await GetZoneId(_logger, domain, apiToken, username);
            if (string.IsNullOrEmpty(zoneId))
            {
                await _logger.Debug("Failed to retrieve zone ID for the domain.");
                return null;
            }

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

            var requestBody = new
            {
                type = "TXT",
                name = "_acme-challenge", // Linode wants just the subdomain part here
                target = content,
                ttl_sec = 120
            };
            var json = JsonSerializer.Serialize(requestBody);

            var contentBody = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PostAsync($"{BaseUrl}/domains/{zoneId}/records", contentBody);
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
        /// Deletes all TXT records for _acme-challenge in the specified Linode DNS zone.
        /// </summary>
        internal static async Task<bool> DeleteAllAcmeChallengeRecords(Logger _logger, string domain, string apiToken, string username)
        {
            string zoneId = await GetZoneId(_logger, domain, apiToken, username);
            if (string.IsNullOrEmpty(zoneId))
            {
                await _logger.Debug("Failed to retrieve zone ID for the domain.");
                return false;
            }

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

            // Get all TXT records in this zone
            var resp = await client.GetAsync($"{BaseUrl}/domains/{zoneId}/records");
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
                var records = doc.RootElement.GetProperty("data");
                foreach (var rec in records.EnumerateArray())
                {
                    string type = rec.GetProperty("type").GetString();
                    string name = rec.GetProperty("name").GetString();
                    int recId = rec.GetProperty("id").GetInt32();

                    if (type == "TXT" && name == "_acme-challenge")
                    {
                        var delResp = await client.DeleteAsync($"{BaseUrl}/domains/{zoneId}/records/{recId}");
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

        /// <summary>
        /// Gets the Linode DNS zone ID for the given domain.
        /// </summary>
        internal static async Task<string> GetZoneId(Logger _logger, string domain, string apiToken, string username)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

            var resp = await client.GetAsync($"{BaseUrl}/domains");
            var respText = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                await _logger.Debug($"[{username}]: Failed to fetch domains: {resp.StatusCode}\n{respText}");
                return null;
            }

            try
            {
                using var doc = JsonDocument.Parse(respText);
                var domains = doc.RootElement.GetProperty("data");
                foreach (var d in domains.EnumerateArray())
                {
                    if (string.Equals(d.GetProperty("domain").GetString(), domain, StringComparison.OrdinalIgnoreCase))
                        return d.GetProperty("id").GetInt32().ToString();
                }
            }
            catch (Exception ex)
            {
                await _logger.Debug($"[{username}]: Exception parsing domains: {ex.Message}");
                return null;
            }

            await _logger.Debug($"[{username}]: Domain not found in Linode account.");
            return null;
        }
    }
}
