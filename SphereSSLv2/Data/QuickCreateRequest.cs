using Newtonsoft.Json;

namespace SphereSSLv2.Data
{
        public class QuickCreateRequest
        {
            [JsonProperty("order")]
            public CertRecord Order { get; set; }

            [JsonProperty("provider")]
            public string Provider { get; set; }

            [JsonProperty("autoAdd")]
            public bool AutoAdd { get; set; }
        }
}
