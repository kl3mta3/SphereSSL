using Newtonsoft.Json;

namespace SphereSSLv2.Models.Dtos
{
    public class UpdateDNSProviderRequest
    {

        [JsonProperty("providerId")]
        public string ProviderId { get; set; } = string.Empty;

        [JsonProperty("providerName")]
        public string ProviderName { get; set; } = string.Empty;

        [JsonProperty("provider")]
        public string Provider { get; set; } = string.Empty;

        [JsonProperty("apiKey")]
        public string APIKey { get; set; } = string.Empty;

        [JsonProperty("ttl")]
        public int Ttl { get; set; }
    }
}
