using SphereSSLv2.Services.Config;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;

namespace SphereSSLv2.Services.APISupportedProviders
{
    public class VultrDNSHelper
    {
        private const string BaseUrl = "https://api.vultr.com/v2";

        /// <summary>
        /// Adds an _acme-challenge TXT DNS record for a domain using the Vultr DNS API.
        /// </summary>
        internal static async Task<string> AddDNSRecord(Logger _logger, string domain, string apiToken, string content, string username)
        {
            var payload = new
            {
                type = "TXT",
                name = "_acme-challenge",
                data = content,
                ttl = 120
            };
            var json = JsonSerializer.Serialize(payload);

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

            var response = await client.PostAsync($"{BaseUrl}/domains/{domain}/records", new StringContent(json, Encoding.UTF8, "application/json"));
            var responseText = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode && responseText.Contains("\"id\""))
            {
                await _logger.Info("DNS record added successfully.");
                return domain;
            }
            else
            {
                await _logger.Debug($"[{username}]: Failed to add DNS record:\n{response.StatusCode}\n{responseText}");
                return null;
            }
        }

        /// <summary>
        /// Deletes all _acme-challenge TXT records for a domain using the Vultr DNS API.
        /// </summary>
        internal static async Task<bool> DeleteAllAcmeChallengeRecords(Logger _logger, string domain, string apiToken, string username)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

            // List all records
            var resp = await client.GetAsync($"{BaseUrl}/domains/{domain}/records");
            var respJson = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                await _logger.Debug($"[{username}]: Failed to fetch DNS records: {resp.StatusCode}\n{respJson}");
                return false;
            }

            var allSuccess = true;
            try
            {
                using var doc = JsonDocument.Parse(respJson);
                if (doc.RootElement.TryGetProperty("records", out var records))
                {
                    foreach (var record in records.EnumerateArray())
                    {
                        var type = record.GetProperty("type").GetString();
                        var name = record.GetProperty("name").GetString();
                        var id = record.GetProperty("id").GetString();

                        if (type == "TXT" && name == "_acme-challenge")
                        {
                            var delResp = await client.DeleteAsync($"{BaseUrl}/domains/{domain}/records/{id}");
                            var delTxt = await delResp.Content.ReadAsStringAsync();
                            if (delResp.IsSuccessStatusCode)
                                await _logger.Info($"Deleted TXT record ID {id}.");
                            else
                            {
                                await _logger.Debug($"[{username}]: Failed to delete TXT record ID {id}: {delResp.StatusCode}\n{delTxt}");
                                allSuccess = false;
                            }
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
