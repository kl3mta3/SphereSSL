using Newtonsoft.Json;

namespace SphereSSLv2.Models.UserModels
{
    public class UserStat
    {
        [JsonProperty("userId")]
        public string UserId { get; set; }

        [JsonProperty("totalCerts")]
        public int TotalCerts { get; set; }

        [JsonProperty("certsRenewed")]
        public int CertsRenewed { get; set; }

        [JsonProperty("certsFailed")]
        public int CertsFailed { get; set; }

        [JsonProperty("lastCertCreated")]
        public DateTime? LastCertCreated { get; set; }
    }
}
