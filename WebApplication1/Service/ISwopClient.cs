using WebApplication1.Models;

namespace WebApplication1.Service
{
    public enum HistoricalInterval
    {
        Week,
        Month
    }

    public interface ISwopClient
    {
        /// <summary>
        /// Získá historické kurzy mezi baseIso a quoteIso
        /// </summary>
        Task<List<HistoricalPoint>> GetHistoricalRatesAsync(string baseIso, string quoteIso, HistoricalInterval interval);

        /// <summary>
        /// Získá aktuální kurz baseIso -> quoteIso
        /// </summary>
        Task<decimal> GetLatestRateAsync(string baseIso, string quoteIso);
        Task<bool> HealthCheckAsync();
    }
}
