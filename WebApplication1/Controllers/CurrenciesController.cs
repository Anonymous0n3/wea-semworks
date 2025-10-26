using Microsoft.AspNetCore.Mvc;
using System;
using WebApplication1.Service;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CurrenciesController : ControllerBase
    {
        private readonly CouchDbService _couch;

        public CurrenciesController(CouchDbService couch)
        {
            _couch = couch;
        }

        /// <summary>
        /// Vrátí všechny měny ze seznamu CouchDB
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var list = await _couch.GetAllCurrenciesAsync();
            return Ok(list);
        }

        /// <summary>
        /// Vrátí konkrétní měnu podle ISO kódu
        /// </summary>
        [HttpGet("{isoCode}")]
        public async Task<IActionResult> Get(string isoCode)
        {
            var cur = await _couch.GetCurrencyAsync(isoCode);
            if (cur == null) return NotFound($"Měna s kódem '{isoCode}' nebyla nalezena.");
            return Ok(cur);
        }
    }
}
