using Newtonsoft.Json;

namespace SphereSSLv2.Models
{
    public class UserRole
    {
        [JsonProperty("userId")]
        public string UserId { get; set; }

        [JsonProperty("isAdmin")]
        public bool IsAdmin { get; set; }

        [JsonProperty("isEnabled")]
        public bool IsEnabled { get; set; }

        [JsonProperty("role")]
        public string Role { get; set; } = "User"; // Viewer, User, Admin, SuperAdmin
    }
}
