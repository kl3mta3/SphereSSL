using System.Text.Json.Serialization;

namespace SphereSSLv2.Models.ConfigModels
{
    public class StoredConfig
    {
        [JsonPropertyName("serverURL")]
        public string ServerURL { get; set; }

        [JsonPropertyName("serverPort")]
        public int ServerPort { get; set; }

        [JsonPropertyName("adminUsername")]
        public string AdminUsername { get; set; }

        [JsonPropertyName("adminPassword")]
        public string AdminPassword { get; set; }

        [JsonPropertyName("databasePath")]
        public string DatabasePath { get; set; }

        [JsonPropertyName("useLogOn")]
        public bool? UseLogOn { get; set; }

        [JsonPropertyName("caPrimeUrl")]
        public string CAPrimeUrl { get; set; }

        [JsonPropertyName("caStagingUrl")]
        public string CAStagingUrl { get; set; }

        [JsonPropertyName("renewBeforeExpiryDays")]
        public int? RenewBeforeExpiryDays { get; set; }

        [JsonPropertyName("certValidityDays")]
        public int? CertValidityDays { get; set; }

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
