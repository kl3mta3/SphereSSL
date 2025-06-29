using System.Text.Json.Serialization;

namespace SphereSSLv2.Models.Dtos
{
    public class CAUpdateRequest
    {

        [JsonPropertyName("caPrimeUrl")]
        public string CAPrimeUrl { get; set; }

        [JsonPropertyName("caStagingUrl")]
        public string CAStagingUrl { get; set; }
    }
}
