using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Globalization;

namespace YourProject.ViewComponents
{
    public class AppInfoWidgetViewComponent : ViewComponent
    {
        public async Task<IViewComponentResult> InvokeAsync()
        {
            var config = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            var logsPath = Environment.GetEnvironmentVariable("APP_LOG_PATH") ?? "/logs";

            var model = new AppInfoViewModel
            {
                AppVersion = typeof(Program).Assembly.GetName().Version?.ToString(),
                DeploymentDate = System.IO.File.GetLastWriteTime(typeof(Program).Assembly.Location),
                MemoryUsage = GetMemoryUsage(),
                CpuUsage = await GetCpuUsageAsync(),
                ProcessCount = GetProcessCount(),
                ApiStatuses = new List<string>(),
                LogsUrl = "/logs" // Controller níže
            };

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
                    // Fallback pro cgroup v1
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
                var cpuUsageFile = "/sys/fs/cgroup/cpu.stat";

                if (!System.IO.File.Exists(cpuUsageFile))
                {
                    cpuUsageFile = "/sys/fs/cgroup/cpuacct.usage";
                }

                var initialUsage = await ReadCpuUsageAsync(cpuUsageFile);
                await Task.Delay(500);
                var finalUsage = await ReadCpuUsageAsync(cpuUsageFile);

                var delta = finalUsage - initialUsage;
                var cpuPercent = (delta / 5_000_000.0).ToString("F2", CultureInfo.InvariantCulture);

                return $"{cpuPercent}%";
            }
            catch
            {
                return "N/A";
            }
        }

        private async Task<long> ReadCpuUsageAsync(string path)
        {
            var text = await System.IO.File.ReadAllTextAsync(path);
            if (text.StartsWith("usage_usec"))
            {
                var usage = text.Split('\n').FirstOrDefault(l => l.StartsWith("usage_usec"));
                return long.Parse(usage?.Split(' ').Last() ?? "0") * 1000; // µs → ns
            }
            else
            {
                return long.Parse(text.Trim());
            }
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
        public string LogsUrl { get; set; } = "";
    }
}
