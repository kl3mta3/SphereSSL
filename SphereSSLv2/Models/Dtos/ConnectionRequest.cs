using System.Text.Json;
using System.Text.Json.Serialization;

namespace SphereSSLv2.Models.Dtos
{
    public class ConnectionRequest
    {
        [JsonPropertyName("connectionId")]
        public string ConnectionId { get; set; } = string.Empty;

        [JsonPropertyName("connectionName")]
        public string ConnectionName { get; set; } = string.Empty;

        [JsonPropertyName("connectionType")]
        public string ConnectionType { get; set; } = string.Empty;

        [JsonPropertyName("isEnabled")]
        public bool IsEnabled { get; set; } = true;

        [JsonPropertyName("onPreRenew")]
        public bool OnPreRenew { get; set; } = true;

        [JsonPropertyName("onPreExpiry")]
        public bool OnPreExpiry { get; set; } = true;

        [JsonPropertyName("onRenewSuccess")]
        public bool OnRenewSuccess { get; set; } = true;

        [JsonPropertyName("onRenewFail")]
        public bool OnRenewFail { get; set; } = true;

        [JsonPropertyName("settings")]
        public JsonElement Settings { get; set; }
    }
}
