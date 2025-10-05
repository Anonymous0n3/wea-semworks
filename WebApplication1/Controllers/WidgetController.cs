using Microsoft.AspNetCore.Mvc;

namespace WebApplication1.Controllers
{
    public class WidgetController : Controller
    {
        public IActionResult Load(string name)
        {
            if (string.IsNullOrEmpty(name))
                return Content("Widget name not provided");

            return ViewComponent(name);
        }
    }
}