using System.Text.Json.Serialization;

namespace SphereSSLv2.Models.Dtos
{
    public class UpdateServerRequest
    {
        [JsonPropertyName("serverUrl")]
        public string ServerUrl { get; set; } 

        [JsonPropertyName("serverPort")]
        public int ServerPort { get; set; }
    }
}
