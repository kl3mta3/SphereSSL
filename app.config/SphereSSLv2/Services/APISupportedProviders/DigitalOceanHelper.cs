using SphereSSLv2.Services.Config;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace SphereSSLv2.Services.APISupportedProviders
{

    public class DigitalOceanHelper
    {
        private const string BaseUrl = "https://api.digitalocean.com/v2/domains";

        /// <summary>
        /// Adds a TXT record to the given domain in DigitalOcean DNS for ACME challenge.
        /// <para><b>API Auth Format:</b> <c>API_TOKEN</c> (just the token string, no colons or extras)</para>
        /// </summary>
        /// <param name="_logger">Logger for info/debug output.</param>
        /// <param name="domain">Root domain (zone) to add the record to.</param>
        /// <param name="apiToken">DigitalOcean Personal Access Token.</param>
        /// <param name="content">TXT value to store.</param>
        /// <param name="username">User context for logging.</param>
        /// <returns>The domain if successful, or null if failed.</returns>
        internal static async Task<string> AddDNSRecord(Logger _logger, string domain, string apiToken, string content, string username)
        {
            if (string.IsNullOrEmpty(apiToken))
            {
                await _logger.Error($"[{username}]: DigitalOcean API token is required.");
                return null;
            }

            string name = $"_acme-challenge.{domain.TrimEnd('.')}";

            var payload = new
            {
                type = "TXT",
                name = name,
                data = content,
                ttl = 120
            };

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

            var json = JsonSerializer.Serialize(payload);
            var contentBody = new StringContent(json, Encoding.UTF8, "application/json");
            var url = $"{BaseUrl}/{domain}/records";

            try
            {
                var response = await client.PostAsync(url, contentBody);
                var responseText = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode && responseText.Contains("\"type\":\"TXT\""))
                {
                    await _logger.Info($"[{username}]: DNS record added successfully.");
                    return domain;
                }
                else
                {
                    await _logger.Debug($"[{username}]: Failed to add DNS record:\n{response.StatusCode}\n{responseText}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                await _logger.Debug($"[{username}]: Exception adding DNS record: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Deletes all _acme-challenge TXT records for the specified domain on DigitalOcean DNS.
        /// <para><b>API Auth Format:</b> <c>API_TOKEN</c> (just the token string)</para>
        /// </summary>
        /// <param name="_logger">Logger for info/debug output.</param>
        /// <param name="domain">Root domain (zone) to delete records from.</param>
        /// <param name="apiToken">DigitalOcean Personal Access Token.</param>
        /// <param name="username">User context for logging.</param>
        /// <returns>True if all deletes succeeded, false if any failed.</returns>
        internal static async Task<bool> DeleteAcmeChallengeRecords(Logger _logger, string domain, string apiToken, string username)
        {
            if (string.IsNullOrEmpty(apiToken))
            {
                await _logger.Error($"[{username}]: DigitalOcean API token is required.");
                return false;
            }

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

            var listUrl = $"https://api.digitalocean.com/v2/domains/{domain}/records";
            var listResponse = await client.GetAsync(listUrl);
            var listJson = await listResponse.Content.ReadAsStringAsync();

            if (!listResponse.IsSuccessStatusCode)
            {
                await _logger.Debug($"[{username}]: Failed to fetch DNS records: {listResponse.StatusCode}\n{listJson}");
                return false;
            }

            try
            {
                using var doc = JsonDocument.Parse(listJson);
                var root = doc.RootElement;
                var records = root.GetProperty("domain_records");

                bool allSuccess = true;

                foreach (var record in records.EnumerateArray())
                {
                    if (record.GetProperty("type").GetString() == "TXT" &&
                        record.GetProperty("name").GetString().StartsWith("_acme-challenge"))
                    {
                        int id = record.GetProperty("id").GetInt32();
                        var delUrl = $"https://api.digitalocean.com/v2/domains/{domain}/records/{id}";
                        var delResp = await client.DeleteAsync(delUrl);

                        if (delResp.IsSuccessStatusCode)
                        {
                            await _logger.Info($"[{username}]: Deleted TXT record ID {id} for _acme-challenge.");
                        }
                        else
                        {
                            var delTxt = await delResp.Content.ReadAsStringAsync();
                            await _logger.Debug($"[{username}]: Failed to delete TXT record ID {id}: {delResp.StatusCode}\n{delTxt}");
                            allSuccess = false;
                        }
                    }
                }

                return allSuccess;
            }
            catch (Exception ex)
            {
                await _logger.Debug($"[{username}]: Exception deleting DNS records: {ex.Message}");
                return false;
            }
        }
    }
}
