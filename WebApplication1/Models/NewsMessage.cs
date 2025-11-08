using System.Text.Json.Serialization;

namespace WebApplication1.Models
{
    public class NewsMessage
    {
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
