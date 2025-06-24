using System.Text.Json.Serialization;

namespace SphereSSLv2.Models.Dtos
{
    public class ToggleUseLogOn
    {
        [JsonPropertyName("username")]
        public string Username { get; set; } = string.Empty;
        [JsonPropertyName("useLogOn")]
        public bool UseLogOn { get; set; } = false;
        [JsonPropertyName("password")]
        public string Password { get; set; } = string.Empty;

    }
}
