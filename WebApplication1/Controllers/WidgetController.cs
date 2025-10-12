using Microsoft.AspNetCore.Mvc;

namespace WebApplication1.Controllers
{
    public class WidgetController : Controller
    {
        // Parametr location se posílá z formuláře
        public IActionResult Load(string name, string? location)
        {
            if (string.IsNullOrEmpty(name))
                return Content("Widget name not provided");

            return ViewComponent(name, new { location });
        }
    }
}
