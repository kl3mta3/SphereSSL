using Newtonsoft.Json;
using System.Configuration.Provider;
using SphereSSLv2.Services.APISupportedProviders;
namespace SphereSSLv2.Data
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

        [JsonProperty("providerName")]
        public string ProviderName { get; set; } = string.Empty;

        [JsonProperty("provider")]
        public string Provider { get; set; } = string.Empty;

        [JsonProperty("aPIKey")]
        public string APIKey { get; set; } = string.Empty;

        [JsonProperty("ttl")]
        public int Ttl { get; set; }



        public static async  Task<bool> TryAutoAddDNS(DNSProvider dnsProvider, string domain, string DnsChallange)
        {
            if (dnsProvider == null)
            {
                throw new ArgumentNullException(nameof(dnsProvider));
            }
            bool success = false;

            ProviderType providerType;

            Enum.TryParse(dnsProvider.Provider, out providerType);

            switch (providerType)
            {
                case ProviderType.Cloudflare:
                    success = await CloudflareHelper.AddOrUpdateDNSRecord(domain, dnsProvider.APIKey, DnsChallange);
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

            return success;
        }
    }
}
