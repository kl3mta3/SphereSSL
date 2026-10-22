using System.Text.Json.Serialization;

namespace SphereSSLv2.Models.Dtos
{
    public class ModeSettingsRequest
    {
        [JsonPropertyName("stagingOnly")]
        public bool? StagingOnly { get; set; }

        [JsonPropertyName("restrictViewers")]
        public bool? RestrictViewers { get; set; }

        [JsonPropertyName("hideViewerLogout")]
        public bool? HideViewerLogout { get; set; }

        [JsonPropertyName("demoLoginEnabled")]
        public bool? DemoLoginEnabled { get; set; }

        [JsonPropertyName("demoUsername")]
        public string DemoUsername { get; set; }

        [JsonPropertyName("demoPassword")]
        public string DemoPassword { get; set; }
    }
}
