using System.Text.Json.Serialization;

namespace WebApplication1.Models
{
    public class HistoricalPoint
    {
        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; }

        [JsonPropertyName("rate")]
        public decimal Rate { get; set; }
    }
}
