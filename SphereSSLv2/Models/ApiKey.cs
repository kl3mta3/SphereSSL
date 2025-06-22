using Newtonsoft.Json;

namespace SphereSSLv2.Models
{
    public class ApiKey
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("userId")]
        public string UserId { get; set; }

        [JsonProperty("apiKey")]
        public string Key { get; set; }

        [JsonProperty("created")]
        public DateTime Created { get; set; }

        [JsonProperty("lastUsed")]
        public DateTime? LastUsed { get; set; }

        [JsonProperty("isRevoked")]
        public bool IsRevoked { get; set; }
    }
}
