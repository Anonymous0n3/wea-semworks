using Microsoft.AspNetCore.Mvc;
using WebApplication1.Service;

namespace WebApplication1.Controllers
{
        [ApiController]
        [Route("api/[controller]")]
        public class SwopController : ControllerBase
        {
            private readonly ISwopClient _swop;
            private readonly CouchDbService _couch;

            public SwopController(ISwopClient swop, CouchDbService couch)
            {
                _swop = swop;
                _couch = couch;
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

                var data = await _swop.GetHistoricalRatesAsync(req.BaseCurrency.ToUpperInvariant(),
                                                               req.QuoteCurrency.ToUpperInvariant(),
                                                               interval);
                return Ok(data);
            }

            /// <summary>
            /// Konverze mezi dvěma měnami podle aktuálního kurzu
            /// </summary>
            [HttpPost("convert")]
            public async Task<IActionResult> Convert([FromBody] ConvertRequest req)
            {
                if (req.Amount < 0) return BadRequest("Amount musí být >= 0.");

                var baseExists = await _couch.GetCurrencyAsync(req.BaseCurrency);
                var quoteExists = await _couch.GetCurrencyAsync(req.QuoteCurrency);
                if (baseExists == null || quoteExists == null)
                    return BadRequest("Neplatný ISO kód - ověřte seznam měn pomocí /api/currencies.");

                var rate = await _swop.GetLatestRateAsync(req.BaseCurrency.ToUpperInvariant(),
                                                           req.QuoteCurrency.ToUpperInvariant());
                var converted = req.Amount * rate;
                return Ok(new { rate, converted });
            }
        }

        public record HistoricalRequest(string BaseCurrency, string QuoteCurrency, string? Interval);
        public record ConvertRequest(string BaseCurrency, string QuoteCurrency, decimal Amount);
    }
