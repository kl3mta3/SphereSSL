using Newtonsoft.Json;

namespace SphereSSLv2.Models.UserModels
{
    public class ApiKey
    {

        [JsonProperty("userId")]
        public string UserId { get; set; }

        [JsonProperty("apiKey")]
        public string APIKey { get; set; }

        [JsonProperty("created")]
        public DateTime Created { get; set; }

        [JsonProperty("lastUsed")]
        public DateTime? LastUsed { get; set; }

        [JsonProperty("isRevoked")]
        public bool IsRevoked { get; set; }
    }
}
