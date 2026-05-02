using System.Text.Json.Serialization;

namespace SphereSSLv2.Models.Dtos
{
    public class BulkOrderRequest
    {
        [JsonPropertyName("orderIds")]
        public List<string> OrderIds { get; set; } = new();
    }
}
