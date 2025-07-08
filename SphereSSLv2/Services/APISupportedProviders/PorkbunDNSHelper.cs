using SphereSSLv2.Services.Config;
using System.Text.Json;
using System.Text;

namespace SphereSSLv2.Services.APISupportedProviders
{
    public class PorkbunDNSHelper
    {
        private const string BaseUrl = "https://porkbun.com/api/json/v3";

        /// <summary>
        /// Adds an _acme-challenge TXT DNS record for a domain using the Porkbun DNS API.
        /// API token format: "APIKey:SecretKey"
        /// </summary>
        internal static async Task<string> AddDNSRecord(Logger _logger, string domain, string apiToken, string content, string username)
        {
            var parts = apiToken.Split(':');
            if (parts.Length != 2)
            {
                await _logger.Error($"[{username}]: API token format invalid. Should be APIKey:SecretKey");
                return null;
            }
            string apiKey = parts[0];
            string secretKey = parts[1];

            var payload = new
            {
                apikey = apiKey,
                secretapikey = secretKey,
                content = content,
                name = "_acme-challenge",
                type = "TXT",
                ttl = 120
            };

            var json = JsonSerializer.Serialize(payload);

            using var client = new HttpClient();
            var response = await client.PostAsync($"{BaseUrl}/dns/create/{domain}/TXT", new StringContent(json, Encoding.UTF8, "application/json"));
            var responseText = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode && responseText.Contains("\"status\":\"SUCCESS\""))
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
        /// Gets all TXT records for a domain using the Porkbun DNS API.
        /// </summary>
        internal static async Task<List<string>> GetAcmeChallengeRecordIds(Logger _logger, string domain, string apiToken, string username)
        {
            var parts = apiToken.Split(':');
            if (parts.Length != 2)
            {
                await _logger.Error($"[{username}]: API token format invalid. Should be APIKey:SecretKey");
                return new List<string>();
            }
            string apiKey = parts[0];
            string secretKey = parts[1];

            var payload = new
            {
                apikey = apiKey,
                secretapikey = secretKey
            };

            var json = JsonSerializer.Serialize(payload);

            using var client = new HttpClient();
            var response = await client.PostAsync($"{BaseUrl}/dns/retrieve/{domain}", new StringContent(json, Encoding.UTF8, "application/json"));
            var responseText = await response.Content.ReadAsStringAsync();

            var ids = new List<string>();
            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(responseText);
                if (doc.RootElement.TryGetProperty("records", out var records))
                {
                    foreach (var record in records.EnumerateArray())
                    {
                        if (record.GetProperty("type").GetString() == "TXT" &&
                            record.GetProperty("name").GetString() == "_acme-challenge")
                        {
                            ids.Add(record.GetProperty("id").GetString());
                        }
                    }
                }
            }
            return ids;
        }

        /// <summary>
        /// Deletes all _acme-challenge TXT DNS records for a domain using the Porkbun DNS API.
        /// </summary>
        internal static async Task<bool> DeleteAllAcmeChallengeRecords(Logger _logger, string domain, string apiToken, string username)
        {
            var ids = await GetAcmeChallengeRecordIds(_logger, domain, apiToken, username);

            var parts = apiToken.Split(':');
            if (parts.Length != 2)
            {
                await _logger.Error($"[{username}]: API token format invalid. Should be APIKey:SecretKey");
                return false;
            }
            string apiKey = parts[0];
            string secretKey = parts[1];

            bool allSuccess = true;

            using var client = new HttpClient();

            foreach (var id in ids)
            {
                var payload = new
                {
                    apikey = apiKey,
                    secretapikey = secretKey
                };
                var json = JsonSerializer.Serialize(payload);

                var response = await client.PostAsync($"{BaseUrl}/dns/delete/{domain}/{id}", new StringContent(json, Encoding.UTF8, "application/json"));
                var responseText = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode && responseText.Contains("\"status\":\"SUCCESS\""))
                {
                    await _logger.Info($"Deleted TXT record ID {id}.");
                }
                else
                {
                    await _logger.Debug($"[{username}]: Failed to delete TXT record ID {id}:\n{response.StatusCode}\n{responseText}");
                    allSuccess = false;
                }
            }
            return allSuccess;
        }
    }
}
