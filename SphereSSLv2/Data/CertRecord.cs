using Newtonsoft.Json;

namespace SphereSSLv2.Data
{
    public class CertRecord
    {
        [JsonProperty("orderId")]
        public string OrderId { get; set; } = string.Empty;

        [JsonProperty("domain")]
        public string Domain { get; set; } = string.Empty;

        [JsonProperty("email")]
        public string Email { get; set; } = string.Empty;

        [JsonProperty("dnsChallengeToken")]
        public string DnsChallengeToken { get; set; } = string.Empty;

        [JsonProperty("savePath")]
        public string SavePath { get; set; } = string.Empty;

        [JsonProperty("provider")]
        public string Provider { get; set; } = string.Empty;

        [JsonProperty("creationTime")]
        public DateTime CreationDate { get; set; } 

        [JsonProperty("expiryDate")]
        public DateTime ExpiryDate { get; set; } 

        [JsonProperty("useSeparateFiles")]
        public bool UseSeparateFiles { get; set; } = false;

        [JsonProperty("saveForRenewal")]
        public bool SaveForRenewal { get; set; } = false;

        [JsonProperty("autoRenew")]
        public bool autoRenew { get; set; } = false;

        [JsonProperty("failedRenewals")]
        public int FailedRenewals { get; set; } = 0;

        [JsonProperty("successfulRenewals")]
        public int SuccessfulRenewals { get; set; } = 0;

        [JsonProperty("zoneId")]
        public string ZoneId { get; set; } = string.Empty;


        [JsonProperty("signer")]
        public string Signer { get; set; } = string.Empty;

        [JsonProperty("accountID")]
        public string AccountID { get; set; } = string.Empty;

        [JsonProperty("orderUrl")]
        public string OrderUrl { get; set; } = string.Empty;

        [JsonProperty("challengeType")]
        public string ChallengeType{ get; set; } = string.Empty;

        [JsonProperty("thumbprint")]
        public string Thumbprint { get; set; } = string.Empty;

        [JsonProperty("authorizationUrls")]
        public List<string> AuthorizationUrls { get; set; } = new();
    }
}
