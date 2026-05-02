using Newtonsoft.Json;

namespace SphereSSLv2.Models.ConnectionModels
{
    public class UserConnection
    {
        [JsonProperty("connectionId")]
        public string ConnectionId { get; set; } = string.Empty;

        [JsonProperty("userId")]
        public string UserId { get; set; } = string.Empty;

        [JsonProperty("connectionName")]
        public string ConnectionName { get; set; } = string.Empty;

        [JsonProperty("connectionType")]
        public string ConnectionType { get; set; } = string.Empty;

        [JsonProperty("isEnabled")]
        public bool IsEnabled { get; set; } = true;

        [JsonProperty("settings")]
        public string Settings { get; set; } = "{}";

        [JsonProperty("onPreRenew")]
        public bool OnPreRenew { get; set; } = true;

        [JsonProperty("onPreExpiry")]
        public bool OnPreExpiry { get; set; } = true;

        [JsonProperty("onRenewSuccess")]
        public bool OnRenewSuccess { get; set; } = true;

        [JsonProperty("onRenewFail")]
        public bool OnRenewFail { get; set; } = true;

        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; }
    }
}
