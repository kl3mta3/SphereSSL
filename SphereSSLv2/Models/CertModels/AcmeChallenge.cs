using Newtonsoft.Json;

namespace SphereSSLv2.Models.CertModels
{
    public class AcmeChallenge
    {
        [JsonProperty("challangeId")]
        public string ChallangeId { get; set; } = string.Empty;

        [JsonProperty("orderId")]
        public string OrderId { get; set; } = string.Empty;

        [JsonProperty("userId")]
        public string UserId { get; set; } = string.Empty;

        [JsonProperty("domain")]
        public string Domain { get; set; } = string.Empty;

        [JsonProperty("dnsChallengeToken")]
        public string DnsChallengeToken { get; set; } = string.Empty;

        [JsonProperty("provider")]
        public string ProviderId { get; set; } = string.Empty;

        [JsonProperty("status")]
        public string Status { get; set; } 

    }
}
