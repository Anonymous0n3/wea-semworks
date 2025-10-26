using System.Text.Json.Serialization;

namespace WebApplication1.Models
{
    public class Currency
    {
        [JsonPropertyName("_id")]
        public string Id { get; set; } = string.Empty;  // ISO 4217 kód, např. "USD"

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty; // Název měny, např. "United States Dollar"

        [JsonPropertyName("type")]
        public string Type { get; set; } = "currency"; // Pro filtrování, default = "currency"
    }
}
