using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Diagnostics;
using System.Globalization;
using WebApplication1.Service;

namespace YourProject.ViewComponents
{
    public class AppInfoWidgetViewComponent : ViewComponent
    {
        private readonly WeatherService _weatherService;
        private readonly IMemoryCache _cache; // Přidáno pro "stateful" výpočet CPU

        public AppInfoWidgetViewComponent(WeatherService weatherService, IMemoryCache cache)
        {
            _weatherService = weatherService;
            _cache = cache;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var model = new AppInfoViewModel
            {
                // Používáme Assembly informaci bezpečněji
                AppVersion = typeof(Program).Assembly.GetName().Version?.ToString(),
                DeploymentDate = System.IO.File.GetLastWriteTime(typeof(Program).Assembly.Location),
                MemoryUsage = GetMemoryUsage(),
                CpuUsage = await GetCpuUsageAsync(), // Nyní bez delaye
                ProcessCount = GetProcessCount(),
                ApiStatuses = new List<string>()
            };

            // --- Health Check ---
            // Pozor: HealthCheck by měl mít timeout, aby nezasekl widget
            try
            {
                // Doporučuji přidat timeout token, pokud ho služba nepodporuje
                bool weatherApiOk = await _weatherService.HealthCheckAsync();
                model.ApiStatuses.Add($"Weather API: {(weatherApiOk ? "✅ Ok" : "❌ Error")}");
            }
            catch
            {
                model.ApiStatuses.Add("Weather API: ⚠️ Timeout/Error");
            }

            return View(model);
        }

        // === PAMĚŤ ===
        private string GetMemoryUsage()
        {
            try
            {
                var usagePath = "/sys/fs/cgroup/memory.current";
                var limitPath = "/sys/fs/cgroup/memory.max";

                // Fallback pro cgroup v1
                if (!System.IO.File.Exists(usagePath))
                {
                    usagePath = "/sys/fs/cgroup/memory/memory.usage_in_bytes";
                    limitPath = "/sys/fs/cgroup/memory/memory.limit_in_bytes";
                }

                if (System.IO.File.Exists(usagePath) && System.IO.File.Exists(limitPath))
                {
                    var usage = long.Parse(System.IO.File.ReadAllText(usagePath).Trim());
                    var limitStr = System.IO.File.ReadAllText(limitPath).Trim();

                    double usageMb = usage / 1024.0 / 1024.0;

                    // Ošetření pro "max", které v cgroup v2 může být "max" textově, nebo obrovské číslo
                    if (limitStr == "max" || !long.TryParse(limitStr, out long limit) || limit > 1_000_000_000_000) // > 1TB považujeme za unlimited
                    {
                        return $"{usageMb:F0} MB (Unlimited)";
                    }

                    double limitMb = limit / 1024.0 / 1024.0;
                    double percent = (double)usage / limit * 100.0;

                    return $"{usageMb:F0} MB / {limitMb:F0} MB ({percent:F1}%)";
                }
            }
            catch { /* Ignorovat chyby čtení */ }

            return "N/A";
        }

        // === CPU (Bez blokování requestu) ===
        private async Task<string> GetCpuUsageAsync()
        {
            try
            {
                // Cesty pro cgroup v2 a v1
                var cpuPathV2 = "/sys/fs/cgroup/cpu.stat"; // Standardní cesta v Dockeru pro v2
                var cpuPathV1 = "/sys/fs/cgroup/cpuacct/cpuacct.usage"; // Starší v1

                long currentUsage = 0;
                long currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(); // Použijeme MS pro měření času

                if (System.IO.File.Exists(cpuPathV2))
                {
                    // Format: "usage_usec 12345\nuser_usec..."
                    var lines = await System.IO.File.ReadAllLinesAsync(cpuPathV2);
                    var usageLine = lines.FirstOrDefault(l => l.StartsWith("usage_usec"));
                    if (usageLine != null)
                    {
                        // usage_usec jsou mikrosekundy
                        currentUsage = long.Parse(usageLine.Split(' ')[1]);
                    }
                }
                else if (System.IO.File.Exists(cpuPathV1))
                {
                    // v1 vrací nanosekundy, převedeme na mikrosekundy (/1000)
                    var text = await System.IO.File.ReadAllTextAsync(cpuPathV1);
                    currentUsage = long.Parse(text.Trim()) / 1000;
                }
                else
                {
                    return "N/A";
                }

                // --- VÝPOČET POMOCÍ CACHE ---
                var cacheKey = "CpuUsage_LastRead";

                // Zkusíme získat minulá data
                if (_cache.TryGetValue(cacheKey, out (long usage, long time) lastRead))
                {
                    var timeDeltaMs = currentTime - lastRead.time;

                    // Pokud od posledního refreshe uběhlo příliš málo času (např. < 100ms), vrátíme cached výsledek, aby čísla neskákala
                    if (timeDeltaMs < 100) return _cache.Get<string>("CpuUsage_LastDisplay") ?? "...";

                    var usageDeltaUsec = currentUsage - lastRead.usage;

                    // Převod času na mikrosekundy: ms * 1000
                    var timeDeltaUsec = timeDeltaMs * 1000.0;
                    var cpuCount = Environment.ProcessorCount;

                    // Vzorec: (Spotřebovaný čas CPU / Uplynulý reálný čas) / Počet jader * 100
                    var percent = (usageDeltaUsec / timeDeltaUsec) / cpuCount * 100.0;

                    // Uložit pro zobrazení
                    var result = $"{percent:F1}%";
                    _cache.Set("CpuUsage_LastDisplay", result, TimeSpan.FromMinutes(1));

                    // Aktualizovat "last read" pro příští request
                    _cache.Set(cacheKey, (currentUsage, currentTime));

                    return result;
                }
                else
                {
                    // První spuštění - nemáme deltu, uložíme aktuální a vrátíme "načítám..."
                    _cache.Set(cacheKey, (currentUsage, currentTime));
                    return "Calc...";
                }
            }
            catch
            {
                return "N/A";
            }
        }

        // === PROCESY ===
        private int GetProcessCount()
        {
            try
            {
                var pidFile = "/sys/fs/cgroup/pids.current"; // cgroup v2
                if (!System.IO.File.Exists(pidFile))
                    pidFile = "/sys/fs/cgroup/pids/pids.current"; // cgroup v1

                if (System.IO.File.Exists(pidFile))
                    return int.Parse(System.IO.File.ReadAllText(pidFile).Trim());

                return Process.GetProcesses().Length;
            }
            catch
            {
                return 0;
            }
        }
    }

    public class AppInfoViewModel
    {
        public string? AppVersion { get; set; }
        public DateTime DeploymentDate { get; set; }
        public string MemoryUsage { get; set; } = "";
        public string CpuUsage { get; set; } = "";
        public int ProcessCount { get; set; }
        public List<string> ApiStatuses { get; set; } = [];
        public string LogsUrl { get; set; } = "/logs";
    }
}
