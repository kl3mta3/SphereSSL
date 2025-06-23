using Newtonsoft.Json;
using System.Configuration.Provider;
using SphereSSLv2.Services.APISupportedProviders;
using SphereSSLv2.Services.Config;

namespace SphereSSLv2.Models.DNSModels
{

    public class DNSProvider
    {


        public enum ProviderType
        {
            Cloudflare,
            DigitalOcean,
            AWSRoute53,
            GoogleCloudDNS,
            Hetzner,
            Namecheap,
            GoDaddy,
            DNSMadeEasy
        }


        [JsonProperty("userId")]
        public string UserId { get; set; } = string.Empty;

        [JsonProperty("providerName")]
        public string ProviderName { get; set; } = string.Empty;

        [JsonProperty("provider")]
        public string Provider { get; set; } = string.Empty;

        [JsonProperty("aPIKey")]
        public string APIKey { get; set; } = string.Empty;

        [JsonProperty("ttl")]
        public int Ttl { get; set; }



        public static async  Task<string> TryAutoAddDNS(Logger _logger, DNSProvider dnsProvider, string domain, string DnsChallange, string username)
        {
            if (dnsProvider == null)
            {
                throw new ArgumentNullException(nameof(dnsProvider));
            }
     

            ProviderType providerType;

            Enum.TryParse(dnsProvider.Provider, out providerType);
            string zoneID = string.Empty;

            switch (providerType)
            {
                case ProviderType.Cloudflare:



                    zoneID = await CloudflareHelper.AddOrUpdateDNSRecord(_logger, domain, dnsProvider.APIKey, DnsChallange, username);
                    break;

                //case ProviderType.DigitalOcean:
                //    success = await DigitalOceanHelper.AddOrUpdateDNSRecord(dnsProvider, domain, DnsChallange);
                //    break;

                //case ProviderType.AWSRoute53:
                //    success = await AWSRoute53Helper.AddOrUpdateDNSRecord(dnsProvider, domain, DnsChallange);
                //    break;

                //case ProviderType.GoogleCloudDNS:
                //    success = await GoogleCloudDNSHelper.AddOrUpdateDNSRecord(dnsProvider, domain, DnsChallange);
                //    break;

                //case ProviderType.Hetzner:
                //    success = await Hetzner.AddOrUpdateDNSRecord(dnsProvider, domain, DnsChallange);
                //    break;

                //case ProviderType.Namecheap:
                //    success = await NamecheapHelper.AddOrUpdateDNSRecord(dnsProvider, domain, DnsChallange);
                //    break;

                //case ProviderType.GoDaddy:
                //    success = await GoDaddyHelper.AddOrUpdateDNSRecord(dnsProvider, domain, DnsChallange);
                //    break;

                //case ProviderType.DNSMadeEasy:
                //    success = await DNSMadeEasy.AddOrUpdateDNSRecord(dnsProvider, domain, DnsChallange);
                //    break;

                default:
                    break;
            }

            return zoneID;
        }
    }
}
