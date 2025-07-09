using SphereSSLv2.Services.Config;
using System.Text.Json;

namespace SphereSSLv2.Services.APISupportedProviders
{
    public class CloudnsnetDNSHelper
    {
        private const string BaseUrl = "https://api.cloudns.net/";


        /// <summary>
        /// Adds a TXT DNS record to Cloudns.net using their API.
        /// Requires the API credentials to be provided as a single string in the format "auth-id:auth-password".
        /// The 'domain' parameter should be the base domain (e.g., "example.com").
        /// The method will split the apiToken to extract the Auth ID and Auth Password.
        /// </summary>
        /// <param name="logger">Logger for output and debugging.</param>
        /// <param name="domain">The base domain name (e.g., "example.com").</param>
        /// <param name="apiToken">API credentials in the format "auth-id:auth-password".</param>
        /// <param name="content">The TXT record value to add.</param>
        /// <param name="username">The username for logging/auditing purposes.</param>
        /// <returns>The zone ID as a string if successful, otherwise null or empty.</returns>
        internal static async Task<string> AddDNSRecord(Logger _logger, string domain, string apiToken, string content, string username)
        {
            var parts = apiToken.Split(':');
            if (parts.Length != 2)
            {
                await _logger.Debug("Invalid Cloudns.net API key format. Must be 'auth-id:auth-password'.");
                return null;
            }
            string authId = parts[0];
            string authPassword = parts[1];

            
            var domainParts = domain.Split('.');
            string domainName = string.Join(".", domainParts.TakeLast(2)); // Example: spheresi.com

            string host = $"_acme-challenge.{domain}".TrimEnd('.');

            var parameters = new Dictionary<string, string>
        {
            { "auth-id", authId },
            { "auth-password", authPassword },
            { "domain-name", domainName },
            { "record-type", "TXT" },
            { "host", host },
            { "record", content },
            { "ttl", "120" }
        };

            using var client = new HttpClient();
            var response = await client.PostAsync(
                $"{BaseUrl}dns/add-record.json",
                new FormUrlEncodedContent(parameters)
            );

            var responseText = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode && responseText.Contains("\"status\":\"Success\"", StringComparison.OrdinalIgnoreCase))
            {
                await _logger.Info($"[Cloudns.net] DNS record added successfully for {domain}.");
                return domainName; // or host, or whatever you want to return
            }
            else
            {
                await _logger.Debug($"[Cloudns.net] Failed to add DNS record:\n{response.StatusCode}\n{responseText}");
                return null;
            }
        }

        /// <summary>
        /// Deletes all TXT records named "_acme-challenge" for the specified domain at Cloudns.net.
        /// <para><b>API Auth Format:</b> <c>AUTHID:PASSWORD</c> (joined with a colon, e.g., "12345:secretpassword").</para>
        /// <para>This will find and remove all TXT records matching the ACME challenge for the domain.</para>
        /// <b>Requirements:</b>
        /// <list type="bullet">
        ///   <item><description>apiToken: Cloudns.net API Auth ID and Password, colon-separated.</description></item>
        ///   <item><description>domain: FQDN whose ACME challenge records should be deleted.</description></item>
        /// </list>
        /// </summary>
        /// <param name="_logger">Logger for info/debug output.</param>
        /// <param name="domain">The domain whose _acme-challenge TXT records will be deleted.</param>
        /// <param name="apiToken">API credentials as "AUTHID:PASSWORD".</param>
        /// <param name="username">User context for logging.</param>
        /// <returns>True if any records were deleted, false if none or on error.</returns>
        internal static async Task<bool> DeleteAllAcmeChallengeRecords(Logger _logger, string domain, string apiToken, string username)
        {
            if (string.IsNullOrEmpty(apiToken) || !apiToken.Contains(":"))
            {
                await _logger.Error($"[{username}]: Cloudns.net API token format invalid. Should be AUTHID:PASSWORD");
                return false;
            }

            string authId = apiToken.Split(':')[0];
            string authPassword = apiToken.Split(':')[1];
            const string baseUrl = "https://api.cloudns.net/";

            try
            {
                // 1. Get all records for the domain
                var getRecordsUrl = $"{baseUrl}dns/records.json?auth-id={authId}&auth-password={authPassword}&domain-name={domain}";
                using var client = new HttpClient();
                var getResp = await client.GetAsync(getRecordsUrl);
                var getRespText = await getResp.Content.ReadAsStringAsync();

                if (!getResp.IsSuccessStatusCode)
                {
                    await _logger.Debug($"[{username}]: Failed to fetch DNS records: {getResp.StatusCode}\n{getRespText}");
                    return false;
                }

                // 2. Parse and find _acme-challenge TXT records
                var records = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(getRespText);
                var acmeRecordIds = new List<string>();
                foreach (var kvp in records)
                {
                    var record = kvp.Value;
                    string host = record.GetProperty("host").GetString();
                    string type = record.GetProperty("type").GetString();
                    if (type == "TXT" && host.StartsWith("_acme-challenge"))
                    {
                        acmeRecordIds.Add(kvp.Key);
                    }
                }

                if (!acmeRecordIds.Any())
                {
                    await _logger.Info($"[{username}]: No _acme-challenge TXT records found to delete for {domain}.");
                    return false;
                }

                // 3. Delete each record
                bool allDeleted = true;
                foreach (string recordId in acmeRecordIds)
                {
                    var deleteUrl = $"{baseUrl}dns/delete-record.json?auth-id={authId}&auth-password={authPassword}&domain-name={domain}&record-id={recordId}";
                    var delResp = await client.GetAsync(deleteUrl);
                    var delText = await delResp.Content.ReadAsStringAsync();

                    if (delResp.IsSuccessStatusCode && delText.Contains("success"))
                    {
                        await _logger.Info($"[{username}]: Deleted _acme-challenge record ID {recordId} for {domain}.");
                    }
                    else
                    {
                        await _logger.Debug($"[{username}]: Failed to delete record ID {recordId}: {delResp.StatusCode} {delText}");
                        allDeleted = false;
                    }
                }

                return allDeleted;
            }
            catch (Exception ex)
            {
                await _logger.Debug($"[{username}]: Exception deleting _acme-challenge records: {ex.Message}");
                return false;
            }
        }

    }

}
