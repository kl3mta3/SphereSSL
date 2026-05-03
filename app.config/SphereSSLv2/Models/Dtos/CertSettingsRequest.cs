using System.Text.Json.Serialization;

namespace SphereSSLv2.Models.Dtos
{
    public class CertSettingsRequest
    {
        [JsonPropertyName("renewBeforeExpiryDays")]
        public int RenewBeforeExpiryDays { get; set; }

        [JsonPropertyName("certValidityDays")]
        public int CertValidityDays { get; set; }
    }
}
