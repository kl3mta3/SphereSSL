using Newtonsoft.Json;

namespace SphereSSLv2.Models.Dtos
{
    public class EditUserRequest
    {

        [JsonProperty("userId")]
        public string UserId { get; set; } // just used to track 

        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("email")]
        public string Email { get; set; }

        [JsonProperty("notes")]
        public string Notes { get; set; }

        [JsonProperty("role")]
        public string Role { get; set; } = "Viewer"; // Default role is viewer
        [JsonProperty("isEnabled")]
        public bool IsEnabled { get; set; } = false; // Default to disabled
    }
}
