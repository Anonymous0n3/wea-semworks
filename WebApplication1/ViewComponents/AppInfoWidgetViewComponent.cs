using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Globalization;
using WebApplication1.Service;

namespace YourProject.ViewComponents
{
    public class AppInfoWidgetViewComponent : ViewComponent
    {
        private readonly WeatherService _weatherService;
        public AppInfoWidgetViewComponent()
        {
            _weatherService = new WeatherService();
        }
        public async Task<IViewComponentResult> InvokeAsync()
        {
            var model = new AppInfoViewModel
            {
                AppVersion = typeof(Program).Assembly.GetName().Version?.ToString(),
                DeploymentDate = System.IO.File.GetLastWriteTime(typeof(Program).Assembly.Location),
                MemoryUsage = GetMemoryUsage(),
                CpuUsage = await GetCpuUsageAsync(),
                ProcessCount = GetProcessCount(),
                ApiStatuses = new List<string>()
            };

            // --- Health Check Weather API ---
            bool weatherApiOk = await _weatherService.HealthCheckAsync();
            model.ApiStatuses.Add($"Weather API: {(weatherApiOk ? "✅ Dostupné" : "❌ Nedostupné")}");

            return View(model);
        }

        // === PAMĚŤ ===
        private string GetMemoryUsage()
        {
            try
            {
                var usagePath = "/sys/fs/cgroup/memory.current";
                var limitPath = "/sys/fs/cgroup/memory.max";

                if (!System.IO.File.Exists(usagePath))
                {
                    // starší cgroup v1 fallback
                    usagePath = "/sys/fs/cgroup/memory/memory.usage_in_bytes";
                    limitPath = "/sys/fs/cgroup/memory/memory.limit_in_bytes";
                }

                var usage = long.Parse(System.IO.File.ReadAllText(usagePath).Trim());
                var limit = long.Parse(System.IO.File.ReadAllText(limitPath).Trim());

                double percent = (double)usage / limit * 100.0;
                return $"{usage / 1024 / 1024} MB / {limit / 1024 / 1024} MB ({percent:F1}%)";
            }
            catch
            {
                return "N/A";
            }
        }

        // === CPU ===
        private async Task<string> GetCpuUsageAsync()
        {
            try
            {
                var cpuUsageFile = "/sys/fs/cgroup/cpu/cpu.stat";
                if (!System.IO.File.Exists(cpuUsageFile))
                    return "N/A";

                var initialUsage = await ReadCpuUsageAsync(cpuUsageFile);
                await Task.Delay(500);
                var finalUsage = await ReadCpuUsageAsync(cpuUsageFile);

                var delta = finalUsage - initialUsage; // mikrosekundy
                var cpuCount = Environment.ProcessorCount;
                var percent = (delta / 500_000_000.0) / cpuCount; // půl sekundy = 500 000 µs

                return $"{percent:F1}%";
            }
            catch
            {
                return "N/A";
            }
        }

        private async Task<long> ReadCpuUsageAsync(string path)
        {
            var lines = await System.IO.File.ReadAllLinesAsync(path);
            var usageLine = lines.FirstOrDefault(l => l.StartsWith("usage_usec"));
            if (usageLine != null)
            {
                return long.Parse(usageLine.Split(' ')[1]);
            }
            return 0;
        }


        // === PROCESY ===
        private int GetProcessCount()
        {
            try
            {
                var pidFile = "/sys/fs/cgroup/pids.current";
                if (!System.IO.File.Exists(pidFile))
                {
                    pidFile = "/sys/fs/cgroup/pids/pids.current";
                }
                var count = int.Parse(System.IO.File.ReadAllText(pidFile).Trim());
                return count;
            }
            catch
            {
                return Process.GetProcesses().Length; // fallback mimo Docker
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
