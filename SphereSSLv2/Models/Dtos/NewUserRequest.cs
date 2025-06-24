using System.Text.Json.Serialization;

namespace SphereSSLv2.Models.Dtos
{
    public class NewUserRequest
    {

        [JsonPropertyName("username")]
        public string Username { get; set; } = string.Empty;
        [JsonPropertyName("password")]
        public string Password { get; set; } = string.Empty;
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;
        [JsonPropertyName("role")]
        public string Role { get; set; } = "User"; // Default role is User
        [JsonPropertyName("note")]
        public string Notes { get; set; } = string.Empty;

    }
}
