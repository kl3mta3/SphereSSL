using Newtonsoft.Json;

namespace SphereSSLv2.Models.ConfigModels
{
    public class LogEntry
    {
        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("logId")]
        public string LogId { get; set; }

        [JsonProperty("alertLevel")]
        public string AlertLevel { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("timestamp")]
        public DateTime Timestamp { get; set; }
    }
}
