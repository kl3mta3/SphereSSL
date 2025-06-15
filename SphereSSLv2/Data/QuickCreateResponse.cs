using Newtonsoft.Json;

namespace SphereSSLv2.Data
{
    public class QuickCreateResponse
    {



        [JsonProperty("order")]
        public CertRecord Order { get; set; }

        [JsonProperty("autoAdd")]
        public bool AutoAdd { get; set; }

        [JsonProperty("autoAdd")]
        public bool AutoAddedSuccessfully { get; set; }
    }
}
