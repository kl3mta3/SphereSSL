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
            Hetzner,
            Namecheap,
            GoDaddy,
            DNSMadeEasy,
            Porkbun,
            Gandi,
            Cloudnsnet,
            DreamHost,
            Vultr,
            Linode,
            DuckDNS
        }


        [JsonProperty("userId")]
        public string UserId { get; set; } = string.Empty;

        [JsonProperty("providerId")]
        public string ProviderId { get; set; } = string.Empty;

        [JsonProperty("username")]
        public string Username { get; set; } = string.Empty;

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
            
            if(domain.Contains("*."))
            {
                domain = domain.Substring(2);
            }

            ProviderType providerType;

            Enum.TryParse(dnsProvider.Provider, out providerType);
            string zoneID = string.Empty;

            switch (providerType)
            {
                case ProviderType.Cloudflare:
                    zoneID = await CloudflareHelper.AddDNSRecord(_logger, domain, dnsProvider.APIKey, DnsChallange, username);
                    break;

                case ProviderType.AWSRoute53:
                    zoneID = await AWSRoute53Helper.AddDNSRecord(_logger, domain, dnsProvider.APIKey, DnsChallange, username);
                    break;

                case ProviderType.DigitalOcean:
                    zoneID = await DigitalOceanHelper.AddDNSRecord(_logger, domain, dnsProvider.APIKey, DnsChallange, username);
                    break;

                case ProviderType.Hetzner:
                    zoneID = await HetznerDNSHelper.AddDNSRecord(_logger, domain, dnsProvider.APIKey, DnsChallange, username);

                    break;

                case ProviderType.Namecheap:
                    zoneID = await NamecheapDNSHelper.AddDNSRecord(_logger, domain, dnsProvider.APIKey, DnsChallange, username);
                    break;

                case ProviderType.GoDaddy:
                    zoneID = await GoDaddyDNSHelper.AddDNSRecord(_logger, domain, dnsProvider.APIKey, DnsChallange, username);
                    break;

                case ProviderType.DNSMadeEasy:
                    zoneID = await DNSMadeEasyDNSHelper.AddDNSRecord(_logger, domain, dnsProvider.APIKey, DnsChallange, username);
                    break;

                case ProviderType.Porkbun:
                    zoneID = await PorkbunDNSHelper.AddDNSRecord(_logger, domain, dnsProvider.APIKey, DnsChallange, username);
                    break;

                case ProviderType.Gandi:
                    zoneID = await GandiDNSHelper.AddDNSRecord(_logger, domain, dnsProvider.APIKey, DnsChallange, username);
                    break;

                case ProviderType.Cloudnsnet:
                    zoneID = await CloudnsnetDNSHelper.AddDNSRecord(_logger, domain, dnsProvider.APIKey, DnsChallange, username);
                    break;

                case ProviderType.DreamHost:
                    zoneID = await DreamHostDNSHelper.AddDNSRecord(_logger, domain, dnsProvider.APIKey, DnsChallange, username);
                    break;

                case ProviderType.Vultr:
                    zoneID = await VultrDNSHelper.AddDNSRecord(_logger, domain, dnsProvider.APIKey, DnsChallange, username);
                    break;

                case ProviderType.Linode:
                    zoneID = await LinodeDNSHelper.AddDNSRecord(_logger, domain, dnsProvider.APIKey, DnsChallange, username);
                    break;

                case ProviderType.DuckDNS:
                    zoneID = await DuckDNSHelper.AddDNSRecord(_logger, domain, dnsProvider.APIKey, DnsChallange, username);
                    break;

                default:
                    break;
            }

            return zoneID;
        }
    }
}
