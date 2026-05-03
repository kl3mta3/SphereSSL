using System.Text.Json.Serialization;

namespace SphereSSLv2.Models.Dtos
{
    public class PasswordChangeRequest
    {
        [JsonPropertyName("userId")]
        public string UserId { get; set; }

        [JsonPropertyName("newPassword")]
        public string NewPassword { get; set; }
    }
}
