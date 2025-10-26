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
        private static readonly Random _rand = new Random();

        public SwopController(ISwopClient swop, CouchDbService couch, SwopCacheService cache)
        {
            _swop = swop;
            _couch = couch;
            _cache = cache;
        }

        // =========================
        // ✅ ENDPOINTY, které očekává widget (GET)
        // =========================

        /// <summary>Pro widget: seznam ISO kódů (datalist/validace)</summary>
        [HttpGet("codes")]
        public IActionResult Codes()
        {
            var list = SupportedEuropeanCurrencyHelper.ToIsoList();
            return Ok(list);
        }

        /// <summary>Pro widget: historie vůči USD (week|month)</summary>
        [HttpGet("history")]
        public async Task<IActionResult> History([FromQuery] string code, [FromQuery] string period = "week")
        {
            if (string.IsNullOrWhiteSpace(code))
                return BadRequest("Missing 'code'.");

            var iso = code.Trim().ToUpperInvariant();
            var allowed = SupportedEuropeanCurrencyHelper.ToIsoList();
            if (!allowed.Contains(iso))
                return BadRequest("Neplatný ISO 4217 kód.");

            var interval = (period?.ToLowerInvariant() == "month")
                ? HistoricalInterval.Month
                : HistoricalInterval.Week;

            // Zadání: historie vůči USD
            var data = await _swop.GetHistoricalRatesAsync("USD", iso, interval);

            // Widget očekává { date: "yyyy-MM-dd", rate: number }
            var shaped = data
                .OrderBy(p => p.Timestamp)
                .Select(p => new { date = p.Timestamp.ToString("yyyy-MM-dd"), rate = p.Rate });

            return Ok(shaped);
        }

        /// <summary>Pro widget: konverze (aktuální kurz)</summary>
        [HttpGet("convert")]
        public async Task<IActionResult> ConvertGet([FromQuery] string from, [FromQuery] string to, [FromQuery] decimal amount = 1m)
        {
            if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
                return BadRequest("Missing 'from' nebo 'to'.");

            var f = from.Trim().ToUpperInvariant();
            var t = to.Trim().ToUpperInvariant();

            var allowed = SupportedEuropeanCurrencyHelper.ToIsoList();
            if (!allowed.Contains(f) || !allowed.Contains(t))
                return BadRequest("Neplatný ISO 4217 kód.");

            var rate = await _swop.GetLatestRateAsync(f, t);
            var converted = Math.Round(amount * rate, 6, MidpointRounding.AwayFromZero);

            return Ok(new
            {
                from = f,
                to = t,
                amount,
                rate,
                converted
            });
        }

        // =========================
        // 📦 Tvoje původní endpointy (ponecháno kvůli kompatibilitě)
        // =========================

        /// <summary>Tvůj původní seznam (alias, zůstává)</summary>
        [HttpGet("currencies")]
        public IActionResult GetSupportedCurrencies()
        {
            var list = SupportedEuropeanCurrencyHelper.ToIsoList();
            return Ok(list);
        }

        /// <summary>Tvůj původní widget POST</summary>
        [HttpPost("widget")]
        public async Task<IActionResult> GetWidgetData([FromBody] WidgetRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.BaseCurrency) || string.IsNullOrWhiteSpace(req.QuoteCurrency))
                return BadRequest("Musíte zadat zdrojovou i cílovou měnu.");

            var baseIso = req.BaseCurrency.ToUpperInvariant();
            var quoteIso = req.QuoteCurrency.ToUpperInvariant();
            var allowed = SupportedEuropeanCurrencyHelper.ToIsoList();

            if (!allowed.Contains(baseIso) || !allowed.Contains(quoteIso))
                return BadRequest("Použijte pouze podporované evropské měny.");

            // 1) current
            decimal currentRate = await _cache.GetOrFetchLatestAsync(baseIso, quoteIso);
            if (currentRate == 0)
            {
                // fallback simulace
                currentRate = Math.Round((decimal)(0.5 + _rand.NextDouble() * 1.5), 4);
            }

            // 2) poslední 3 dny
            var today = DateTime.UtcNow.Date;
            var last3 = new List<HistoricalPoint>();

            for (int i = 1; i <= 3; i++)
            {
                var day = today.AddDays(-i);
                var point = await _cache.GetOrFetchHistoricalDateAsync(baseIso, quoteIso, day);

                if (point == null)
                {
                    // simulace ±5 %
                    var fakeRate = Math.Round(currentRate * (1 - 0.05m + (decimal)_rand.NextDouble() * 0.1m), 4);
                    point = new HistoricalPoint { Timestamp = day, Rate = fakeRate };
                }

                last3.Add(point);
            }

            var diffs = last3.Select(p => (currentRate - p.Rate) / p.Rate * 100m).ToList();

            decimal volatility = 0m;
            if (diffs.Count > 1)
            {
                var avg = diffs.Average();
                var variance = diffs.Sum(d => (d - avg) * (d - avg)) / (diffs.Count - 1);
                volatility = Math.Round((decimal)Math.Sqrt((double)variance), 4);
            }

            return Ok(new
            {
                Base = baseIso,
                Quote = quoteIso,
                CurrentRate = currentRate,
                Historical = last3.Select(x => new { Date = x.Timestamp.ToString("yyyy-MM-dd"), Rate = x.Rate }),
                PercentDiffs = diffs,
                Volatility = volatility
            });
        }

        /// <summary>Původní POST konverze</summary>
        [HttpPost("convert")]
        public async Task<IActionResult> ConvertPost([FromBody] ConvertRequest req)
        {
            if (req.Amount < 0)
                return BadRequest("Amount musí být >= 0.");

            var rate = await _swop.GetLatestRateAsync(
                req.BaseCurrency.ToUpperInvariant(),
                req.QuoteCurrency.ToUpperInvariant()
            );

            var converted = req.Amount * rate;
            return Ok(new { rate, converted });
        }

        /// <summary>Původní POST historie</summary>
        [HttpPost("historical")]
        public async Task<IActionResult> HistoricalPost([FromBody] HistoricalRequest req)
        {
            if (string.IsNullOrEmpty(req.BaseCurrency) || req.BaseCurrency.Length != 3 ||
                string.IsNullOrEmpty(req.QuoteCurrency) || req.QuoteCurrency.Length != 3)
                return BadRequest("ISO kódy musí být třípísmenné (např. USD, EUR).");

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
    }

    // Request DTOs
    public record HistoricalRequest(string BaseCurrency, string QuoteCurrency, string? Interval);
    public record ConvertRequest(string BaseCurrency, string QuoteCurrency, decimal Amount);
    public record WidgetRequest(string BaseCurrency, string QuoteCurrency);
}
