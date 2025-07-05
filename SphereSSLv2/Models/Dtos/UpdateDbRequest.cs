using System.Text.Json.Serialization;

namespace SphereSSLv2.Models.Dtos
{
    public class UpdateDbRequest
    {
        [JsonPropertyName("dbPath")]
        public string DbPath { get; set; }
    }
}
