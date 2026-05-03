using System.Text.Json.Serialization;

namespace SphereSSLv2.Models.Dtos
{
    public class KeyExchangeRequest
    {

        [JsonPropertyName("certPem")]
        public string CertPem { get; set; } = string.Empty;

        [JsonPropertyName("keyPem")]
        public string KeyPem { get; set; } = string.Empty;

        [JsonPropertyName("keyFile")]
        public string KeyFile { get; set; } = string.Empty;

        [JsonPropertyName("outputType")]
        public string OutputType { get; set; } = string.Empty;

        [JsonPropertyName("password")]
        public string Password { get; set; } = string.Empty;

    }
}
