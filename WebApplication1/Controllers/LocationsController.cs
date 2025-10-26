using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using WebApplication1.Service;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/[controller]")] // => /api/locations
    public class LocationsController : Controller
    {
        private readonly WeatherService _weatherService;

        public LocationsController(WeatherService weatherService)
        {
            _weatherService = weatherService ?? throw new ArgumentNullException(nameof(weatherService));
        }

        /// <summary>
        /// Autocomplete pro vyhledávání měst (WeatherAPI /search.json).
        /// Příklad: GET /api/locations?q=Pra
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Get([FromQuery(Name = "q")] string q)
        {
            if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
                return Json(Array.Empty<object>());

            var results = await _weatherService.SearchLocationAsync(q.Trim());

            var simplified = results.Select(r => new
            {
                label = string.IsNullOrWhiteSpace(r.Region)
                    ? $"{r.Name}, {r.Country}"
                    : $"{r.Name}, {r.Region}, {r.Country}",
                // query = přesně tohle můžeš poslat do inputu (funguje i pro WeatherAPI dotazy)
                query = string.IsNullOrWhiteSpace(r.Region)
                    ? $"{r.Name}, {r.Country}"
                    : $"{r.Name}, {r.Region}, {r.Country}",
                name = r.Name,
                region = r.Region,
                country = r.Country,
                lat = r.Lat,
                lon = r.Lon
            });

            return Json(simplified);
        }
    }
}
