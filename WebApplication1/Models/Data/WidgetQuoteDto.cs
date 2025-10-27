using WebApplication1.Service;

namespace WebApplication1.Models.Data
{
    public class WidgetQuoteDto
    {
        public string Base { get; set; } = "";
        public string Quote { get; set; } = "";
        public decimal CurrentRate { get; set; }

        // Array of last 3 days (date + rate). Could be fewer if data chybí.
        public List<HistoricalPoint> Last3Days { get; set; } = new();

        // procentni rozdily: current vs each of previous days (e.g. [pctFromDayMinus1, pctFromDayMinus2, pctFromDayMinus3])
        public List<decimal> PercentDiffs { get; set; } = new();

        // volatility = standard deviation of PercentDiffs
        public decimal Volatility { get; set; }
    }

}
