using Microsoft.Extensions.Caching.Memory;
using WebApplication1.Controllers;
using WebApplication1.Models;

namespace WebApplication1.Service
{
    public class SwopCacheService
    {
        private readonly IMemoryCache _cache;
        private readonly ISwopClient _swopClient;
        private readonly TimeSpan _oneDay = TimeSpan.FromDays(1);
        private readonly ILogger<SwopCacheService> _logger;

        public SwopCacheService(IMemoryCache cache, ISwopClient swopClient, ILogger<SwopCacheService> logger)
        {
            _cache = cache;
            _swopClient = swopClient;
            _logger = logger;
        }

        // key examples:
        // latest: "latest:EUR:GBP:2025-10-19"
        // historical: "hist:EUR:GBP:2025-10-18"
        private static string LatestKey(string baseIso, string quoteIso, DateTime date) =>
            $"latest:{baseIso}:{quoteIso}:{date:yyyy-MM-dd}";

        private static string HistKey(string baseIso, string quoteIso, DateTime date) =>
            $"hist:{baseIso}:{quoteIso}:{date:yyyy-MM-dd}";

        // Vrátí cached latest (decimal) nebo zavolá SWOP a uloží do cache do půlnoci
        public async Task<decimal> GetOrFetchLatestAsync(string baseIso, string quoteIso)
        {
            var today = DateTime.UtcNow.Date;
            var key = LatestKey(baseIso, quoteIso, today);

            if (_cache.TryGetValue<decimal>(key, out var cached)) return cached;

            var rate = await _swopClient.GetLatestRateAsync(baseIso, quoteIso);

            // expirace do půlnoci UTC (nebo lokálně pokud chceš)
            var expiresAt = DateTime.UtcNow.Date.AddDays(1);
            var ttl = expiresAt - DateTime.UtcNow;

            _cache.Set(key, rate, ttl);
            return rate;
        }

        // Vrátí cached historical pro konkrétní date (date param je DateTime.Date)
        public async Task<HistoricalPoint?> GetOrFetchHistoricalDateAsync(string baseIso, string quoteIso, DateTime date)
        {
            var d = date.Date;
            var key = HistKey(baseIso, quoteIso, d);

            if (_cache.TryGetValue<HistoricalPoint>(key, out var cached))
                return cached;

            var list = await _swopClient.GetHistoricalRatesAsync(baseIso, quoteIso, HistoricalInterval.Week);
            var point = list.FirstOrDefault(p => p.Timestamp.Date == d);

            // Pokud je požadovaný den v minulosti → cacheujeme např. 7 dní
            TimeSpan ttl;
            var expiresAt = d.AddDays(1);
            var diff = expiresAt - DateTime.UtcNow;

            if (diff > TimeSpan.Zero)
                ttl = diff; // budoucí nebo dnešní den
            else
                ttl = TimeSpan.FromDays(7); // minulý den – statické historické data

            if (point != null)
            {
                _cache.Set(key, point, ttl);
            }
            else
            {
                _cache.Set(key, point, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = ttl
                });
            }

            return point;
        }
    }
}
