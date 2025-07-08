using System.Text.Json.Serialization;

namespace SphereSSLv2.Models.Dtos
{
    public class LocalCertRequest
    {
        [JsonPropertyName("subjectName")]
        public string subjectName { get; set; } = string.Empty;

        [JsonPropertyName("sanNames")]
        public List<string> sanNames { get; set; } = new List<string>();

        [JsonPropertyName("password")]
        public string password { get; set; } = string.Empty;

        [JsonPropertyName("validDays")]
        public int validDays { get; set; } = 365;
    }
}
