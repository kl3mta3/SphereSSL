using Newtonsoft.Json;

namespace SphereSSLv2.Models.ConfigModels
{
    public class HealthStat
    {
        [JsonProperty("totalCertsInDB")]
        public int TotalCertsInDB { get; set; } = 0;

        [JsonProperty("expiredCertCount")]
        public int ExpiredCertCount { get; set; } = 0;

        [JsonProperty("totalDNSProviderCount")]
        public int TotalDNSProviderCount { get; set; } = 0;

        [JsonProperty("dateLastBooted")]
        public string DateLastBooted { get; set; } = string.Empty;
    }
}
