using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using WebApplication1.Models;

namespace WebApplication1.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        // TOTO JE DASHBOARD (Pracovní plocha)
        public IActionResult Index()
        {
            return View();
        }

        // TOTO JE GALERIE (Rozcestník / Veøejné widgety)
        public IActionResult PublicGallery()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        // Helper pro Currency widget reload
        [HttpGet]
        public IActionResult CurrencyWidget(string baseCurrency = "EUR", string quoteCurrency = "USD")
        {
            return RedirectToAction("Index", new { baseCurrency, quoteCurrency });
        }
    }
}