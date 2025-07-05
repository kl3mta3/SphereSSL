using System.Text.Json.Serialization;

namespace SphereSSLv2.Models.Dtos
{
    public class OrderRenewRequest
    {
        [JsonPropertyName("orderId")]
        public string OrderId { get; set; }
    }
}
