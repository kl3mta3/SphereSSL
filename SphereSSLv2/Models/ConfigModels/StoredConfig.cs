using System.Text.Json.Serialization;

namespace SphereSSLv2.Models.ConfigModels
{
    public class StoredConfig
    {
        [JsonPropertyName("serverURL")]
        public string ServerURL {get; set; }

        [JsonPropertyName("serverPort")]
        public int ServerPort { get; set; }

        [JsonPropertyName("adminUsername")]
        public string AdminUsername { get; set; }

        [JsonPropertyName("adminPassword")]
        public string AdminPassword { get; set; }

        [JsonPropertyName("databasePath")]
        public string DatabasePath { get; set; }

        [JsonPropertyName("useLogOn ")]
        public bool UseLogOn { get; set; }
    }
}
