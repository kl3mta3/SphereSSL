using Newtonsoft.Json;
using SphereSSLv2.Models.CertModels;


namespace SphereSSLv2.Models.Dtos
{
    public class QuickCreateRequest
    {
        [JsonProperty("order")]
        public CertRecord Order { get; set; }

        [JsonProperty("provider")]
        public string Provider { get; set; }

        [JsonProperty("autoAdd")]
        public bool AutoAdd { get; set; }

        [JsonProperty("useStaging")]
        public bool UseStaging { get; set; }
    }
}
