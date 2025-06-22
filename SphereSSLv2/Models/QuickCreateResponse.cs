using Newtonsoft.Json;

namespace SphereSSLv2.Models
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
