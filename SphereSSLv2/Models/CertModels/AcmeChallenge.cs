using Newtonsoft.Json;

namespace SphereSSLv2.Models.CertModels
{
    public class AcmeChallenge
    {
        [JsonProperty("orderId")]
        public string OrderId { get; set; } = string.Empty;

        [JsonProperty("userId")]
        public string UserId { get; set; } = string.Empty;

        [JsonProperty("domain")]
        public string Domain { get; set; } = string.Empty;

        [JsonProperty("dnsChallengeToken")]
        public string DnsChallengeToken { get; set; } = string.Empty;

        public string Status { get; set; } 

    }
}
