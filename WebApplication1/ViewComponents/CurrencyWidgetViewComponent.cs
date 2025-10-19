using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using WebApplication1.Models;

namespace WebApplication1.Views.Shared.Components.CurrencyWidget
{
    public class CurrencyWidgetViewComponent : ViewComponent
    {
        private readonly IHttpClientFactory _factory;

        public CurrencyWidgetViewComponent(IHttpClientFactory factory)
        {
            _factory = factory;
        }

        public async Task<IViewComponentResult> InvokeAsync(string baseCurrency = "EUR", string quoteCurrency = "USD")
        {
            var client = _factory.CreateClient();

            // Použij přímo podporované měny
            var currencies = SupportedEuropeanCurrencyHelper.ToIsoList()
                               .Select(c => new Currency { Id = c, Name = c }) // Id a Name stejné
                               .ToList();

            // Zavolej backend widget endpoint
            var req = new { BaseCurrency = baseCurrency, QuoteCurrency = quoteCurrency };
            var resp = await client.PostAsJsonAsync("/api/swop/widget", req);

            decimal rate = 0, volatility = 0;
            List<(DateTime Date, decimal Rate)> history = new();

            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadAsStringAsync();
                var root = JsonDocument.Parse(json).RootElement;

                rate = root.GetProperty("CurrentRate").GetDecimal();
                volatility = root.GetProperty("Volatility").GetDecimal();

                foreach (var item in root.GetProperty("Historical").EnumerateArray())
                {
                    var t = DateTime.Parse(item.GetProperty("Timestamp").GetString() ?? DateTime.UtcNow.ToString());
                    var r = item.GetProperty("Rate").GetDecimal();
                    history.Add((t, r));
                }
            }

            ViewData["BaseCurrency"] = baseCurrency;
            ViewData["QuoteCurrency"] = quoteCurrency;
            ViewData["Rate"] = rate;
            ViewData["Volatility"] = volatility;
            ViewData["History"] = history;

            return View("Default", currencies);
        }
    }

    // Jednoduchý model pro View
    public class Currency
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }
}
