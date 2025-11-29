using System.Text.Json.Serialization;

namespace WebApplication1.Models
{
    public class PublicWidgetDoc
    {
        [JsonPropertyName("_id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonPropertyName("_rev")]
        public string Rev { get; set; }

        public string Type { get; set; } = "public_widget"; // Diskriminátor pro CouchDB

        public string WidgetType { get; set; } // Např. "Weather", "Currency"
        public string PublicName { get; set; } // Název pro vyhledávání
        public string AuthorEmail { get; set; }
        public string AuthorName { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int LikesCount { get; set; } = 0;
        public List<string> LikedBy { get; set; } = new List<string>(); // Seznam emailů pro zamezení duplicitních like

        // Uložíme nastavení widgetu (např. lokace, měny)
        public UserWidgetState WidgetData { get; set; }
    }

    // Pomocná třída pro filtrování
    public class WidgetFilterRequest
    {
        public string? WidgetType { get; set; }
        public string? SearchName { get; set; }
        public string? Author { get; set; }
        public string SortBy { get; set; } = "date"; // "date" nebo "likes"
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
}