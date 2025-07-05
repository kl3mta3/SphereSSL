using System.Text.Json.Serialization;

namespace SphereSSLv2.Models.Dtos
{
    public class ProviderDeleteRequest
    {
        [JsonPropertyName("providerId")]
        public string ProviderId { get; set; }
    }
}
