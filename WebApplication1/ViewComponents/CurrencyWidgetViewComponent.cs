using Microsoft.AspNetCore.Mvc;
using WebApplication1.Models;
using WebApplication1.Service;

namespace WebApplication1.Views.Shared.Components.CurrencyWidget
{
    public class CurrencyWidgetViewComponent : ViewComponent
    {
        private readonly ISwopClient _swop;
        private readonly SwopCacheService _cache;

        public CurrencyWidgetViewComponent(ISwopClient swop, SwopCacheService cache)
        {
            _swop = swop;
            _cache = cache;
        }

        // baseCurrency/quoteCurrency přijímáme jako parametry, defaulty jsou EUR/USD
        public async Task<IViewComponentResult> InvokeAsync(string baseCurrency = "EUR", string quoteCurrency = "USD")
        {
            var query = HttpContext.Request.Query;

            if (query.ContainsKey("baseCurrency"))
                baseCurrency = query["baseCurrency"].ToString().ToUpperInvariant();

            if (query.ContainsKey("quoteCurrency"))
                quoteCurrency = query["quoteCurrency"].ToString().ToUpperInvariant();

            var currencies = SupportedEuropeanCurrencyHelper.ToIsoList();

            decimal currentRate = 0m;
            try
            {
                currentRate = await _swop.GetLatestRateAsync(baseCurrency, quoteCurrency);
                if (currentRate == 0m)
                {
                    Console.WriteLine($"[CurrencyWidget] Chybná hodnota kurzu pro {baseCurrency} → {quoteCurrency}: data chybí nebo účet neumožňuje přístup.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CurrencyWidget] Chyba při načítání aktuálního kurzu {baseCurrency} → {quoteCurrency}: {ex.Message}");
            }

            var today = DateTime.UtcNow.Date;
            var history = new List<(DateTime Date, decimal Rate)>();

            // Pouze poslední 3 dny
            for (int i = 3; i >= 1; i--)
            {
                var day = today.AddDays(-i);
                decimal dayRate = 0m;
                try
                {
                    var h = await _cache.GetOrFetchHistoricalDateAsync(baseCurrency, quoteCurrency, day);
                    if (h?.Rate != null && h.Timestamp.Date == day)
                    {
                        dayRate = h.Rate;
                    }
                    else
                    {
                        Console.WriteLine($"[CurrencyWidget] Chybná historická data pro {baseCurrency} → {quoteCurrency} ({day:yyyy-MM-dd}): data chybí nebo účet neumožňuje přístup.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[CurrencyWidget] Výjimka při načítání historického kurzu {baseCurrency} → {quoteCurrency} ({day:yyyy-MM-dd}): {ex.Message}");
                }

                history.Add((day, dayRate));
            }

            // Volatilita – ignoruje nulové hodnoty
            var diffs = history.Where(p => p.Rate != 0m).Select(p => currentRate != 0m ? (currentRate - p.Rate) / p.Rate * 100m : 0m).ToList();
            decimal volatility = 0m;
            if (diffs.Count > 0)
            {
                var avg = diffs.Average();
                var variance = diffs.Sum(d => (d - avg) * (d - avg)) / diffs.Count;
                volatility = (decimal)Math.Sqrt((double)variance);
                volatility = Math.Round(volatility, 4);
            }

            ViewData["BaseCurrency"] = baseCurrency;
            ViewData["QuoteCurrency"] = quoteCurrency;
            ViewData["Rate"] = currentRate;
            ViewData["Volatility"] = volatility;
            ViewData["History"] = history;

            return View("Default", currencies);
        }


    }
}
