using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Json;
using WebApplication1.Models;
using System.Text.Json;

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

            // 🪙 1️⃣ Načti seznam měn z CouchDB (přes tvůj controller)
            var currencies = await client.GetFromJsonAsync<List<Currency>>("https://localhost:5001/api/currencies")
                              ?? new List<Currency>();

            // 📊 2️⃣ Zavolej GraphQL endpoint (Swop API přes tvůj backend)
            var req = new
            {
                BaseCurrency = baseCurrency,
                QuoteCurrency = quoteCurrency
            };

            var resp = await client.PostAsJsonAsync("https://localhost:5001/api/swop/widget", req);

            // defaultní hodnoty
            decimal rate = 0;
            decimal volatility = 0;
            List<(DateTime Date, decimal Rate)> history = new();

            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                rate = root.GetProperty("CurrentRate").GetDecimal();
                volatility = root.GetProperty("Volatility").GetDecimal();

                foreach (var item in root.GetProperty("Historical").EnumerateArray())
                {
                    var t = DateTime.Parse(item.GetProperty("Timestamp").GetString() ?? DateTime.Now.ToString());
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
}
