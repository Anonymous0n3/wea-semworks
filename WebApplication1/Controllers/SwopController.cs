using Microsoft.AspNetCore.Mvc;
using WebApplication1.Service;
using WebApplication1.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SwopController : ControllerBase
    {
        private readonly ISwopClient _swop;
        private readonly CouchDbService _couch;
        private readonly SwopCacheService _cache;

        public SwopController(ISwopClient swop, CouchDbService couch, SwopCacheService cache)
        {
            _swop = swop;
            _couch = couch;
            _cache = cache;
        }

        /// <summary>
        /// Získá historická data kurzu mezi dvěma měnami
        /// </summary>
        [HttpPost("historical")]
        public async Task<IActionResult> Historical([FromBody] HistoricalRequest req)
        {
            if (string.IsNullOrEmpty(req.BaseCurrency) || req.BaseCurrency.Length != 3 ||
                string.IsNullOrEmpty(req.QuoteCurrency) || req.QuoteCurrency.Length != 3)
                return BadRequest("ISO kódy musí být třípísmenné (např. USD, EUR).");

            var baseExists = await _couch.GetCurrencyAsync(req.BaseCurrency);
            var quoteExists = await _couch.GetCurrencyAsync(req.QuoteCurrency);
            if (baseExists == null || quoteExists == null)
                return BadRequest("Neplatný ISO kód - ověřte seznam měn pomocí /api/currencies.");

            var interval = req.Interval?.ToLowerInvariant() == "month"
                ? HistoricalInterval.Month
                : HistoricalInterval.Week;

            var data = await _swop.GetHistoricalRatesAsync(
                req.BaseCurrency.ToUpperInvariant(),
                req.QuoteCurrency.ToUpperInvariant(),
                interval
            );

            return Ok(data);
        }

        /// <summary>
        /// Konverze mezi dvěma měnami podle aktuálního kurzu
        /// </summary>
        [HttpPost("convert")]
        public async Task<IActionResult> Convert([FromBody] ConvertRequest req)
        {
            if (req.Amount < 0)
                return BadRequest("Amount musí být >= 0.");

            var baseExists = await _couch.GetCurrencyAsync(req.BaseCurrency);
            var quoteExists = await _couch.GetCurrencyAsync(req.QuoteCurrency);
            if (baseExists == null || quoteExists == null)
                return BadRequest("Neplatný ISO kód - ověřte seznam měn pomocí /api/currencies.");

            var rate = await _swop.GetLatestRateAsync(
                req.BaseCurrency.ToUpperInvariant(),
                req.QuoteCurrency.ToUpperInvariant()
            );

            var converted = req.Amount * rate;
            return Ok(new { rate, converted });
        }

        /// <summary>
        /// Vrací podporované evropské měny (pro frontend dropdowny)
        /// </summary>
        [HttpGet("currencies")]
        public IActionResult GetSupportedCurrencies()
        {
            var list = SupportedEuropeanCurrencyHelper.ToIsoList();
            return Ok(list);
        }

        /// <summary>
        /// Endpoint pro měnový widget – aktuální kurz + volatilita za 3 dny
        /// </summary>
        [HttpPost("widget")]
        public async Task<IActionResult> GetWidgetData([FromBody] WidgetRequest req)
        {
            var baseIso = req.BaseCurrency.ToUpperInvariant();
            var quoteIso = req.QuoteCurrency.ToUpperInvariant();

            var allowed = SupportedEuropeanCurrencyHelper.ToIsoList();
            if (!allowed.Contains(baseIso) || !allowed.Contains(quoteIso))
                return BadRequest("Použijte pouze podporované evropské měny.");

            // 1️⃣ Aktuální kurz (z cache)
            var current = await _cache.GetOrFetchLatestAsync(baseIso, quoteIso);

            // 2️⃣ Poslední 3 dny z historických dat (z cache)
            var today = DateTime.UtcNow.Date;
            var last3 = new List<HistoricalPoint>();

            for (int i = 1; i <= 3; i++)
            {
                var day = today.AddDays(-i);
                var point = await _cache.GetOrFetchHistoricalDateAsync(baseIso, quoteIso, day);
                if (point != null)
                    last3.Add(point);
            }

            // 3️⃣ Procentní rozdíly (current vs každý den)
            var diffs = last3
                .Where(p => p.Rate != 0)               // vyhneme se dělení nulou
                .Select(p => (current - p.Rate) / p.Rate * 100m)
                .ToList();

            // 4️⃣ Výpočet volatility (směrodatná odchylka)
            decimal volatility = 0m;
            if (diffs.Count > 0)
            {
                var avg = diffs.Average();
                var variance = diffs.Sum(d => (d - avg) * (d - avg)) / diffs.Count; // variance
                volatility = (decimal)Math.Sqrt((double)variance);                 // směrodatná odchylka
            }

            // Zaokrouhlení pro hezké zobrazení
            volatility = Math.Round(volatility, 4);

            var response = new
            {
                Base = baseIso,
                Quote = quoteIso,
                CurrentRate = current,
                Historical = last3.Select(x => new { x.Timestamp, x.Rate }),
                PercentDiffs = diffs,
                Volatility = volatility
            };

            return Ok(response);
        }
    }

    public record HistoricalRequest(string BaseCurrency, string QuoteCurrency, string? Interval);
    public record ConvertRequest(string BaseCurrency, string QuoteCurrency, decimal Amount);
    public record WidgetRequest(string BaseCurrency, string QuoteCurrency);
}
