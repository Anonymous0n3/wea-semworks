using System.Text.Json.Serialization;

namespace WebApplication1.Models
{
    public class NewsMessage
    {
        // Metadata pro CouchDB
        [JsonPropertyName("_id")]
        public string _id { get; set; } = Guid.NewGuid().ToString(); // Automatické generování ID

        [JsonPropertyName("_rev")]
        public string? _rev { get; set; }

        // Pevný typ dokumentu pro filtrování
        [JsonPropertyName("Type")]
        public string Type { get; set; } = "news_message";

        // Původní vlastnosti
        [JsonPropertyName("link")]
        public string Link { get; set; }

        [JsonPropertyName("headline")]
        public string Title { get; set; }

        [JsonPropertyName("category")]
        public string Category { get; set; }

        [JsonPropertyName("short_description")]
        public string ShortDescription { get; set; }

        [JsonPropertyName("authors")]
        public string Authors { get; set; }

        [JsonPropertyName("date")]
        public string Date { get; set; }
    }
}
