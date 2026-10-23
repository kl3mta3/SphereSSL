using Newtonsoft.Json;


namespace SphereSSLv2.Models.CertModels
{
    public class RevokedCertRecord
    {

        [JsonProperty("orderId")]
        public string OrderId { get; set; } = string.Empty;

        [JsonProperty("userId")]
        public string UserId { get; set; } = string.Empty;

        [JsonProperty("email")]
        public string Email { get; set; } = string.Empty;

        [JsonProperty("challenges")]
        public List<AcmeChallenge> Challenges { get; set; } = new();

        [JsonProperty("savePath")]
        public string SavePath { get; set; } = string.Empty;

        [JsonProperty("creationTime")]
        public DateTime CreationDate { get; set; }

        [JsonProperty("expiryDate")]
        public DateTime ExpiryDate { get; set; }

        [JsonProperty("revokeDate")]
        public DateTime RevokeDate { get; set; }

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

        [JsonProperty("signer")]
        public string Signer { get; set; } = string.Empty;

        [JsonProperty("accountID")]
        public string AccountID { get; set; } = string.Empty;

        [JsonProperty("orderUrl")]
        public string OrderUrl { get; set; } = string.Empty;

        [JsonProperty("challengeType")]
        public string ChallengeType { get; set; } = string.Empty;

        [JsonProperty("thumbprint")]
        public string Thumbprint { get; set; } = string.Empty;
    }
}
