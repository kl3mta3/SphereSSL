using Newtonsoft.Json;
using SphereSSLv2.Models.CertModels;

namespace SphereSSLv2.Models.Dtos
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
