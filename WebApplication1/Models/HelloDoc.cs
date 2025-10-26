using System.Text.Json.Serialization;

namespace WebApplication1.Models
{
    public class HelloDoc
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? _id { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? _rev { get; set; }

        public string text { get; set; } = "";
    }
}
