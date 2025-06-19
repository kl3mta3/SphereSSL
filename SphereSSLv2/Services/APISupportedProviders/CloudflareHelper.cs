using SphereSSLv2.Data;
using System.DirectoryServices.ActiveDirectory;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace SphereSSLv2.Services.APISupportedProviders
{
    public class CloudflareHelper
    {
        private const string BaseUrl = "https://api.cloudflare.com/client/v4";



        public  static async Task<bool> TestAPIKey(Logger _logger, string apiKey)
        {
            var url = $"{BaseUrl}/verify";
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            try
            {
                var response = await client.GetAsync(url);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _= _logger.Debug($"Cloudflare API check failed: {response.StatusCode}\n{responseContent}");
                    return false;
                }

                using var doc = JsonDocument.Parse(responseContent);
                var root = doc.RootElement;

                bool success = root.GetProperty("success").GetBoolean();

                return success;
            }
            catch (Exception ex)
            {
                _= _logger.Debug($"Error verifying token: {ex.Message}");
                return false;
            }
        }

        public static async Task<string> GetZoneId(Logger _logger, string apiToken, string domain)
        {
            using var client = new HttpClient();

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

            var response = await client.GetAsync($"{BaseUrl}/zones?name={domain}");

            if (!response.IsSuccessStatusCode)
            {
                _= _logger.Debug($"Failed to fetch zone: {response.StatusCode}");
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.GetProperty("success").GetBoolean() && root.GetProperty("result").GetArrayLength() > 0)
                {
                    return root.GetProperty("result")[0].GetProperty("id").GetString();
                }
                else
                {
                    _= _logger.Debug("No zone found matching that domain.");
                    return null;
                }
            }
            catch (JsonException ex)
            {
                _= _logger.Debug($"JSON parse error: {ex.Message}");
                return null;
            }
        }

        public static async Task<string> GetDnsRecordId(Logger _logger, string apiToken, string zoneId, string name)
        {
            using var client = new HttpClient();
            string type = "TXT";
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

            var response = await client.GetAsync($"{BaseUrl}/zones/{zoneId}/dns_records?type={type}&name={name}");
            var responseText = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(responseText);
                var root = doc.RootElement;

                if (root.GetProperty("success").GetBoolean() && root.GetProperty("result").GetArrayLength() > 0)
                {
                    var record = root.GetProperty("result")[0];
                    return record.GetProperty("id").GetString();
                }
            }

            _= _logger.Debug($"Failed to retrieve DNS record ID:\n{response.StatusCode}\n{responseText}");
            return null;
        }

        public static async Task<string> AddOrUpdateDNSRecord(Logger _logger, string domain, string apiToken, string content)
        {
            string zoneId = await GetZoneId(_logger, apiToken, domain);
            if (string.IsNullOrEmpty(zoneId)) return String.Empty;

            string name = $"_acme-challenge.{domain}";
            string recordId = await GetDnsRecordId(_logger, apiToken, zoneId, name);


            if (string.IsNullOrEmpty(recordId))
            {

                await AddDNSRecord(_logger, domain, apiToken, content);
                return zoneId;
            }
            else
            {
               
                
                await UpdateDNSRecord(_logger, domain, apiToken, content);
                return zoneId;
            }
        }

        private static async Task<bool> AddDNSRecord(Logger _logger, string domain, string apiToken, string content, int ttl = 120)
        {

            bool proxied = false;
            string type = "TXT";
            string zoneId = await GetZoneId(_logger, apiToken, domain);
            string name = $"_acme-challenge.{domain}";
            if (string.IsNullOrEmpty(zoneId))
            {
                _= _logger.Debug("Failed to retrieve zone ID for the domain.");
                return false;
            }

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

            var requestBody = new
            {
                type,
                name,
                content,
                ttl,
                proxied
            };

            var json = JsonSerializer.Serialize(requestBody);
            var contentBody = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync($"{BaseUrl}/zones/{zoneId}/dns_records", contentBody);
            var responseText = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                _= _logger.Info("DNS record added successfully.");
                return true;
            }
            else
            {
                _= _logger.Debug($"Failed to add DNS record:\n{response.StatusCode}\n{responseText}");
                return false;
            }
        }

        private static async Task<bool> UpdateDNSRecord(Logger _logger, string domain, string apiToken, string content)


        {
            int ttl = 120;
            bool proxied = false;
            string type = "TXT";
            string zoneId = await GetZoneId(_logger, apiToken, domain);
            string name = $"_acme-challenge.{domain}";
            string recordId = await GetDnsRecordId(_logger, apiToken, zoneId, name);

            if (string.IsNullOrEmpty(zoneId))
            {
                _= _logger.Debug("Failed to retrieve zone ID for the domain.");
                return false;
            }

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

            var requestBody = new
            {
                type,
                name,
                content,
                ttl,
                proxied
            };

            var json = JsonSerializer.Serialize(requestBody);
            var contentBody = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PutAsync($"{BaseUrl}/zones/{zoneId}/dns_records/{recordId}", contentBody);
            var responseText = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                _= _logger.Info("DNS record updated successfully.");
                return true;
            }
            else
            {
                _= _logger.Debug($"Failed to update DNS record:\n{response.StatusCode}\n{responseText}");
                return false;
            }
        }

        public  async Task<bool> DeleteDNSRecord(Logger _logger, string apiToken, string domain)
        {
            string zoneId = await GetZoneId(_logger, apiToken, domain);
            if (string.IsNullOrEmpty(zoneId))
            {
                _= _logger.Debug("Failed to retrieve zone ID for the domain.");
                return false;
            }

            string name = $"_acme-challenge.{domain}";
            string recordId = await GetDnsRecordId(_logger, apiToken, zoneId, name);
            if (string.IsNullOrEmpty(recordId))
            {
                _= _logger.Debug("No matching DNS record found to delete.");
                return false;
            }

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

            var response = await client.DeleteAsync($"{BaseUrl}/zones/{zoneId}/dns_records/{recordId}");
            var responseText = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                _= _logger.Info("DNS record deleted successfully.");
                return true;
            }
            else
            {
                _= _logger.Debug($"Failed to delete DNS record:\n{response.StatusCode}\n{responseText}");
                return false;
            }
        }
    }
}
