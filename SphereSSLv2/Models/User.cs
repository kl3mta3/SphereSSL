using Newtonsoft.Json;

namespace SphereSSLv2.Models
{
    public class User
    {
        [JsonProperty("id")]
        public int Id { get; set; } // SQLite row ID

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
        public string CreationTime { get; set; }

        [JsonProperty("lastUpdated")]
        public string LastUpdated { get; set; }

        [JsonProperty("uuid")]
        public string UUID { get; set; } // Public-safe UUID

        [JsonProperty("notes")]
        public string Notes { get; set; }
    }
}
