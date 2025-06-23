using Newtonsoft.Json;

namespace SphereSSLv2.Models.UserModels
{
    public class User
    {
        [JsonProperty("userId")]
        public string UserId { get; set; } // Secure internal ID (Guid in hex)

        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("passwordHash")]
        public string PasswordHash { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("email")]
        public string Email { get; set; }

        [JsonProperty("creationTime")]
        public DateTime CreationTime { get; set; }

        [JsonProperty("lastUpdated")]
        public DateTime? LastUpdated { get; set; }

        [JsonProperty("uuid")]
        public string UUID { get; set; } // Public-safe UUID

        [JsonProperty("notes")]
        public string Notes { get; set; }
    }
}
