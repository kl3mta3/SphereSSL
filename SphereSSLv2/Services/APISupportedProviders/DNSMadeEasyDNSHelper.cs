using SphereSSLv2.Services.Config;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace SphereSSLv2.Services.APISupportedProviders
{
    public class DNSMadeEasyDNSHelper
    {
        private const string BaseUrl = "https://api.dnsmadeeasy.com/V2.0";

        /// <summary>
        /// Gets the Zone ID for a domain from DNS Made Easy using their REST API.
        /// Requires apiToken in "KEY:SECRET" format. HMAC signing is mandatory.
        /// </summary>
        /// <param name="_logger">Logger instance for status output.</param>
        /// <param name="domain">The full domain name to search for.</param>
        /// <param name="apiToken">API credentials in "KEY:SECRET" format.</param>
        /// <param name="username">User performing the operation (for logging).</param>
        /// <returns>The Zone ID as a string, or null if not found or failed.</returns>
        internal static async Task<string> GetZoneId(Logger _logger, string domain, string apiToken, string username)
        {
            var parts = apiToken.Split(':');
            if (parts.Length != 2)
            {
                await _logger.Error($"[{username}]: API token format invalid. Should be KEY:SECRET");
                return null;
            }
            var apiKey = parts[0];
            var apiSecret = parts[1];

            var now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
            string method = "GET";
            string pathAndQuery = "/V2.0/dns/managed"; // NOTE: No trailing slash, matches API docs!
            string body = ""; // GET request, so no body
            string hmac = ComputeDnsmeHmac(apiSecret, now, method, pathAndQuery, body);

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("x-dnsme-apiKey", apiKey);
            client.DefaultRequestHeaders.Add("x-dnsme-requestDate", now);
            client.DefaultRequestHeaders.Add("x-dnsme-hmac", hmac);

            var domainsResp = await client.GetAsync($"{BaseUrl}dns/managed");
            var domainsJson = await domainsResp.Content.ReadAsStringAsync();

            if (!domainsResp.IsSuccessStatusCode)
            {
                await _logger.Debug($"[{username}]: Failed to fetch domain list: {domainsResp.StatusCode}\n{domainsJson}");
                return null;
            }

            try
            {
                using var doc = JsonDocument.Parse(domainsJson);
                foreach (var domainEntry in doc.RootElement.GetProperty("data").EnumerateArray())
                {
                    if (string.Equals(domainEntry.GetProperty("name").GetString(), domain, StringComparison.OrdinalIgnoreCase))
                        return domainEntry.GetProperty("id").GetInt32().ToString();
                }
            }
            catch (Exception ex)
            {
                await _logger.Debug($"[{username}]: Exception parsing domain list: {ex.Message}");
                return null;
            }

            await _logger.Debug($"[{username}]: Domain not found in DNS Made Easy account.");
            return null;
        }

        /// <summary>
        /// Adds a TXT DNS record (for ACME challenge) to DNS Made Easy for the specified domain.
        /// </summary>
        /// <param name="_logger">Logger for info/debug output.</param>
        /// <param name="domain">Root domain (zone) to add record to.</param>
        /// <param name="apiToken">API Key and Secret (format: key:secret).</param>
        /// <param name="content">TXT value to add.</param>
        /// <param name="username">User context for logging.</param>
        /// <returns>ZoneId if success, null if failed.</returns>
        internal static async Task<string> AddDNSRecord(Logger _logger, string domain, string apiToken, string content, string username)
        {
            string zoneId = await GetZoneId(_logger, domain, apiToken, username);
            if (string.IsNullOrEmpty(zoneId))
            {
                await _logger.Debug("Failed to retrieve zone ID for the domain.");
                return null;
            }
            var parts = apiToken.Split(':');
            var apiKey = parts[0];
            var apiSecret = parts[1];

            var requestBody = new
            {
                type = "TXT",
                name = $"_acme-challenge.{domain.TrimEnd('.')}",
                value = content,
                ttl = 120
            };
            var json = JsonSerializer.Serialize(requestBody);

            var now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
            string method = "POST";
            string pathAndQuery = $"/V2.0/dns/managed/{zoneId}/records"; // No trailing slash!
            string hmac = ComputeDnsmeHmac(apiSecret, now, method, pathAndQuery, json);

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("x-dnsme-apiKey", apiKey);
            client.DefaultRequestHeaders.Add("x-dnsme-requestDate", now);
            client.DefaultRequestHeaders.Add("x-dnsme-hmac", hmac);

            var contentBody = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PostAsync($"{BaseUrl}dns/managed/{zoneId}/records", contentBody); // No trailing slash
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
        /// Deletes all _acme-challenge TXT records from DNS Made Easy for the specified domain.
        /// </summary>
        /// <param name="_logger">Logger for info/debug output.</param>
        /// <param name="domain">Root domain (zone) to delete records from.</param>
        /// <param name="apiToken">API Key and Secret (format: key:secret).</param>
        /// <param name="username">User context for logging.</param>
        /// <returns>True if all deletes succeeded, false otherwise.</returns>
        internal static async Task<bool> DeleteAcmeChallengeRecords(Logger _logger, string domain, string apiToken, string username)
        {
            string zoneId = await GetZoneId(_logger, domain, apiToken, username);
            if (string.IsNullOrEmpty(zoneId))
            {
                await _logger.Debug("Failed to retrieve zone ID for the domain.");
                return false;
            }
            var parts = apiToken.Split(':');
            var apiKey = parts[0];
            var apiSecret = parts[1];

            // ---- GET TXT RECORDS ----
            string now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
            string getMethod = "GET";
            string getPath = $"/V2.0/dns/managed/{zoneId}/records?type=TXT";
            string getHmac = ComputeDnsmeHmac(apiSecret, now, getMethod, getPath, "");
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("x-dnsme-apiKey", apiKey);
            client.DefaultRequestHeaders.Add("x-dnsme-requestDate", now);
            client.DefaultRequestHeaders.Add("x-dnsme-hmac", getHmac);

            var recordsResp = await client.GetAsync($"{BaseUrl}dns/managed/{zoneId}/records?type=TXT");
            var recordsJson = await recordsResp.Content.ReadAsStringAsync();

            if (!recordsResp.IsSuccessStatusCode)
            {
                await _logger.Debug($"[{username}]: Failed to fetch TXT records: {recordsResp.StatusCode}\n{recordsJson}");
                return false;
            }

            bool allSuccess = true;
            try
            {
                using var doc = JsonDocument.Parse(recordsJson);
                var records = doc.RootElement.GetProperty("data");
                foreach (var rec in records.EnumerateArray())
                {
                    string name = rec.GetProperty("name").GetString();
                    int recId = rec.GetProperty("id").GetInt32();
                    if (name.StartsWith("_acme-challenge", StringComparison.OrdinalIgnoreCase))
                    {
                        // ---- DELETE RECORD ----
                        string delNow = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
                        string delMethod = "DELETE";
                        string delPath = $"/V2.0/dns/managed/{zoneId}/records/{recId}";
                        string delHmac = ComputeDnsmeHmac(apiSecret, delNow, delMethod, delPath, "");

                        using var delClient = new HttpClient();
                        delClient.DefaultRequestHeaders.Add("x-dnsme-apiKey", apiKey);
                        delClient.DefaultRequestHeaders.Add("x-dnsme-requestDate", delNow);
                        delClient.DefaultRequestHeaders.Add("x-dnsme-hmac", delHmac);

                        var delResp = await delClient.DeleteAsync($"{BaseUrl}dns/managed/{zoneId}/records/{recId}");
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

        private static string ComputeDnsmeHmac(string apiSecret, string date, string method, string pathAndQuery, string body = "")
        {
            // HMAC string to sign
            var stringToSign = $"{date}{method.ToUpper()}{pathAndQuery}{body}";
            var key = Encoding.UTF8.GetBytes(apiSecret);
            var data = Encoding.UTF8.GetBytes(stringToSign);

            using var hmac = new System.Security.Cryptography.HMACSHA1(key);
            var hash = hmac.ComputeHash(data);
            return Convert.ToBase64String(hash);
        }
    }
}
