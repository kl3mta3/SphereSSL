using Newtonsoft.Json;
using System.Configuration.Provider;

namespace SphereSSLv2.Data
{
    public class DNSProvider
    {
        [JsonProperty("providerName")]
        public string ProviderName  { get; set; } = string.Empty;

        [JsonProperty("providerURL")]
        public string ProviderURL { get; set; } = string.Empty;

        [JsonProperty("aPIKey")]
        public string APIKey  { get; set; } = string.Empty;

        [JsonProperty("ttl")]
        public int Ttl { get; set; }

        [JsonProperty("updateAPI ")]
        public string UpdateAPI { get; set; } = string.Empty;

        [JsonProperty("createAPI")]
        public string CreateAPI { get; set; } = string.Empty;

        [JsonProperty("deleteAPI ")]
        public string DeleteAPI  { get; set; } = string.Empty;



    }
}
