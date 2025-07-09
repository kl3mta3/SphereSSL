
using SphereSSLv2.Services.Config;
using Amazon.Route53;
using Amazon.Route53.Model;

namespace SphereSSLv2.Services.APISupportedProviders
{
    public class AWSRoute53Helper
    {
        private const string BaseUrl = "https://route53.amazonaws.com/2013-04-01/";

        public static async Task<string> GetRoute53ZoneIdAsync(string accessKey, string secretKey, string domain)
        {
            using var client = new AmazonRoute53Client(accessKey, secretKey, Amazon.RegionEndpoint.USEast1);
            string fqdn = domain.TrimEnd('.') + "."; // Route53 hosted zone names always end with dot
            string bestMatchId = null;
            int bestMatchLength = -1;

            var resp = await client.ListHostedZonesAsync();

            foreach (var zone in resp.HostedZones)
            {
                if (fqdn.EndsWith(zone.Name, StringComparison.OrdinalIgnoreCase) && zone.Name.Length > bestMatchLength)
                {
                    bestMatchId = zone.Id.Replace("/hostedzone/", ""); 
                    bestMatchLength = zone.Name.Length;
                }
            }

            return bestMatchId; 
        }
        
        /// <summary>
        /// Adds a DNS TXT record to AWS Route 53 for the specified domain.
        /// Requires API credentials in the format "ACCESSKEY:SECRETKEY" as a single string.
        /// The method splits the apiToken to extract the AWS Access Key and Secret Key.
        /// Automatically finds the correct hosted zone ID for the given domain and attempts to upsert the TXT record.
        /// </summary>
        /// <param name="_logger">Logger for status and debugging output.</param>
        /// <param name="domain">The domain for which to add the TXT record (e.g., "example.com").</param>
        /// <param name="apiToken">API credentials in the format "ACCESSKEY:SECRETKEY".</param>
        /// <param name="content">The value of the TXT record to be added.</param>
        /// <param name="username">The username for logging and auditing purposes.</param>
        /// <returns>The zone ID as a string if successful, otherwise null.</returns>
        internal static async Task<string> AddDNSRecord(Logger _logger, string domain, string apiToken, string content, string username)
        {
            string[] parts = apiToken.Split(':');
            if (parts.Length != 2)
            {
                await _logger.Error($"[{username}]: API token format invalid. Should be ACCESSKEY:SECRETKEY");
                return null;
            }
            string apiKey = parts[0];
            string apiSecret = parts[1];
            using var client = new AmazonRoute53Client(apiKey, apiSecret, Amazon.RegionEndpoint.USEast1);

            string zoneId = await GetRoute53ZoneIdAsync(apiKey, apiSecret, domain);
            if (string.IsNullOrEmpty(zoneId))
            {
                _ = _logger.Debug("Failed to retrieve zone ID for the domain.");
                return null;
            }

            string recordName = $"_acme-challenge.{domain.TrimEnd('.')}.";
            var request = new ChangeResourceRecordSetsRequest
            {
                HostedZoneId = zoneId,
                ChangeBatch = new ChangeBatch
                {
                    Changes = new List<Change>
                    {
                        new Change
                        {
                            Action = ChangeAction.UPSERT,
                            ResourceRecordSet = new ResourceRecordSet
                            {
                                Name = recordName,
                                Type = RRType.TXT,
                                TTL = 120,
                                ResourceRecords = new List<ResourceRecord>
                                {
                                    new ResourceRecord { Value = $"\"{content}\"" }
                                }
                            }
                        }
                    }
                }
            };

            try
            {
                var response = await client.ChangeResourceRecordSetsAsync(request);

                if (response.HttpStatusCode == System.Net.HttpStatusCode.OK)
                {
                    await _logger.Info("DNS record added successfully.");
                    return zoneId;
                }
                else
                {
                    await _logger.Debug($"[{username}]: Failed to add DNS record: StatusCode={response.HttpStatusCode}");
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
        /// Deletes all DNS TXT records named "_acme-challenge.{domain}" in the given AWS Route 53 zone.
        /// Requires API credentials in the format "ACCESSKEY:SECRETKEY" as a single string.
        /// </summary>
        /// <param name="_logger">Logger for debug/info output.</param>
        /// <param name="domain">Domain to search (e.g., "example.com").</param>
        /// <param name="apiToken">API credentials as "ACCESSKEY:SECRETKEY".</param>
        /// <param name="zoneId">Hosted zone ID where records should be deleted.</param>
        /// <param name="username">Username for logging context.</param>
        /// <returns>True if any records were deleted, false otherwise.</returns>
        internal static async Task<bool> DeleteAcmeChallengeRecords(Logger _logger, string domain, string apiToken, string zoneId, string username)
        {
            string[] parts = apiToken.Split(':');
            if (parts.Length != 2)
            {
                await _logger.Error($"[{username}]: API token format invalid. Should be ACCESSKEY:SECRETKEY");
                return false;
            }
            string apiKey = parts[0];
            string apiSecret = parts[1];

            using var client = new Amazon.Route53.AmazonRoute53Client(apiKey, apiSecret, Amazon.RegionEndpoint.USEast1);
            string recordName = $"_acme-challenge.{domain.TrimEnd('.')}.";

            try
            {
                // Fetch all TXT records in the zone
                var listReq = new ListResourceRecordSetsRequest
                {
                    HostedZoneId = zoneId
                };

                var listResp = await client.ListResourceRecordSetsAsync(listReq);

                var acmeRecords = listResp.ResourceRecordSets
                    .Where(rr => rr.Type == RRType.TXT && rr.Name.TrimEnd('.') == recordName.TrimEnd('.'))
                    .ToList();

                if (!acmeRecords.Any())
                {
                    await _logger.Info($"[{username}]: No _acme-challenge TXT records found to delete.");
                    return false;
                }

                var changes = acmeRecords.Select(rr => new Change
                {
                    Action = ChangeAction.DELETE,
                    ResourceRecordSet = rr
                }).ToList();

                var delReq = new ChangeResourceRecordSetsRequest
                {
                    HostedZoneId = zoneId,
                    ChangeBatch = new ChangeBatch { Changes = changes }
                };

                var delResp = await client.ChangeResourceRecordSetsAsync(delReq);

                if (delResp.HttpStatusCode == System.Net.HttpStatusCode.OK)
                {
                    await _logger.Info($"[{username}]: Deleted {acmeRecords.Count} _acme-challenge TXT record(s) in zone {zoneId}.");
                    return true;
                }
                else
                {
                    await _logger.Debug($"[{username}]: Failed to delete _acme-challenge TXT records: StatusCode={delResp.HttpStatusCode}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                await _logger.Debug($"[{username}]: Exception deleting _acme-challenge records: {ex.Message}");
                return false;
            }
        }
    }
}
