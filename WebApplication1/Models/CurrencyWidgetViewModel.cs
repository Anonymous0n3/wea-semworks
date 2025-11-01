using System;
using System.Collections.Generic;
using WebApplication1.Models.Data;

namespace WebApplication1.Models
{
    public class CurrencyWidgetViewModel
    {
        public IReadOnlyList<string> SupportedCurrencies { get; set; } = SupportedEuropeanCurrencyHelper.ToIsoList();

        // Data pro sub-widget
        public string? SelectedBase { get; set; }
        public string? SelectedQuote { get; set; }
        public decimal? CurrentRate { get; set; }
        public decimal? Volatility { get; set; }
        public List<HistoricalPoint>? Last3Days { get; set; } = new();
        public List<decimal>? PercentDiffs { get; set; } = new();
    }
}
