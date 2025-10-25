// Models/UserDoc.cs
namespace WebApplication1.Models
{
    public class UserDoc
    {
        public string? _id { get; set; }
        public string? _rev { get; set; }
        public string Type { get; set; } = "user";
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
        public string PasswordHash { get; set; } = "";
        public string DashboardStateJson { get; set; } = "[]"; // nové pole pro widgety
        public List<UserWidgetState>? OpenWidgets { get; set; }
    }
}
