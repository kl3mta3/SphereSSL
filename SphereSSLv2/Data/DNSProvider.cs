using Newtonsoft.Json;
using System.Configuration.Provider;

namespace SphereSSLv2.Data
{
    public class DNSProvider
    {
        [JsonProperty("providerName")]
        public string ProviderName  { get; set; } = string.Empty;

        [JsonProperty("providerBaseURL")]
        public string ProviderBaseURL { get; set; } = string.Empty;

        [JsonProperty("aPIKey")]
        public string APIKey  { get; set; } = string.Empty;

        [JsonProperty("ttl")]
        public int Ttl { get; set; }

    }
}
