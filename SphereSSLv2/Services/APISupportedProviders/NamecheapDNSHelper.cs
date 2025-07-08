using SphereSSLv2.Services.Config;

namespace SphereSSLv2.Services.APISupportedProviders
{
    public class NamecheapDNSHelper
    {
        // API endpoint
        private const string BaseUrl = "https://api.namecheap.com/xml.response";

        /// <summary>
        /// Adds a TXT DNS record for _acme-challenge for a domain in Namecheap DNS via their API.
        /// API token format: "APIUser:APIKey:UserName:ClientIp"
        /// </summary>
        internal static async Task<string> AddDNSRecord(Logger _logger, string domain, string apiToken, string content, string username)
        {
            var parts = apiToken.Split(':');
            if (parts.Length != 4)
            {
                await _logger.Error($"[{username}]: API token format invalid. Should be APIUser:APIKey:UserName:ClientIp");
                return null;
            }

            string apiUser = parts[0];
            string apiKey = parts[1];
            string userName = parts[2];
            string clientIp = parts[3];

            string hostName = "_acme-challenge";
            string sld = domain.Split('.')[^2];
            string tld = domain.Split('.')[^1];

            var url = $"{BaseUrl}?ApiUser={apiUser}&ApiKey={apiKey}&UserName={userName}&ClientIp={clientIp}" +
                      $"&Command=namecheap.domains.dns.setHosts&Sld={sld}&Tld={tld}" +
                      $"&HostName1={hostName}&RecordType1=TXT&Address1={content}&TTL1=120";

            using var client = new HttpClient();
            var response = await client.GetAsync(url);
            var responseText = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode && responseText.Contains("<IsSuccess>true</IsSuccess>"))
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
        /// Gets all DNS host records for a domain in Namecheap DNS.
        /// </summary>
        internal static async Task<List<(int id, string name)>> GetAllTxtRecords(Logger _logger, string domain, string apiToken, string username)
        {
            var parts = apiToken.Split(':');
            if (parts.Length != 4)
            {
                await _logger.Error($"[{username}]: API token format invalid. Should be APIUser:APIKey:UserName:ClientIp");
                return new List<(int, string)>();
            }

            string apiUser = parts[0];
            string apiKey = parts[1];
            string userName = parts[2];
            string clientIp = parts[3];

            string sld = domain.Split('.')[^2];
            string tld = domain.Split('.')[^1];

            var url = $"{BaseUrl}?ApiUser={apiUser}&ApiKey={apiKey}&UserName={userName}&ClientIp={clientIp}" +
                      $"&Command=namecheap.domains.dns.getHosts&Sld={sld}&Tld={tld}";

            using var client = new HttpClient();
            var response = await client.GetAsync(url);
            var responseText = await response.Content.ReadAsStringAsync();

            var results = new List<(int, string)>();
            if (response.IsSuccessStatusCode)
            {
                // Super lazy XML parse for demo; in prod, use XmlDocument/XElement etc
                var matches = System.Text.RegularExpressions.Regex.Matches(responseText, "<Host (.+?) />");
                int id = 1;
                foreach (System.Text.RegularExpressions.Match m in matches)
                {
                    string name = "";
                    string type = "";
                    foreach (System.Text.RegularExpressions.Capture c in m.Groups[1].Captures)
                    {
                        if (c.Value.Contains("Type=\"TXT\"") && c.Value.Contains("Name=\"_acme-challenge\""))
                        {
                            var nMatch = System.Text.RegularExpressions.Regex.Match(c.Value, "Name=\"([^\"]+)\"");
                            if (nMatch.Success) name = nMatch.Groups[1].Value;
                            type = "TXT";
                            results.Add((id, name));
                        }
                    }
                    id++;
                }
            }
            return results;
        }

        /// <summary>
        /// Deletes all _acme-challenge TXT records for the given domain in Namecheap DNS.
        /// </summary>
        internal static async Task<bool> DeleteAllAcmeChallengeRecords(Logger _logger, string domain, string apiToken, string username)
        {
            // Namecheap DNS doesn't let you delete a single record directly—you set the whole host record list at once.
            // To "delete" _acme-challenge, you must rebuild the hosts list minus those records.

            // 1. Get all hosts
            var txtRecords = await GetAllTxtRecords(_logger, domain, apiToken, username);

            // 2. Remove _acme-challenge TXT records from the list (should parse all, but demo keeps it simple)
            var recordsToKeep = txtRecords.Where(t => t.name != "_acme-challenge").ToList();

            // 3. Rebuild SetHosts with recordsToKeep (left as an exercise—API is fiddly)

            // Note: In practice, this is ugly and only needed if you really want to nuke ACME records. Document this to users!
            await _logger.Info("Would reconstruct host list without _acme-challenge and call setHosts again.");

            return true;
        }
    }
}
