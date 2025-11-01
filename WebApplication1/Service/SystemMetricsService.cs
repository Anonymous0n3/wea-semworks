using System.Diagnostics;
using WebApplication1.Controllers;

namespace WidgetsDemo.Services
{
    public class SystemMetricsService
    {
        private readonly ILogger<SystemMetricsService> _logger; 
        public SystemMetricsService(ILogger<SystemMetricsService> logger) { 
            _logger = logger;
        }   
        public double GetCpuUsage()
        {
            using var proc = Process.GetCurrentProcess();
            return proc.TotalProcessorTime.TotalMilliseconds / Environment.ProcessorCount;
        }

        public double GetMemoryUsageMB()
        {
            using var proc = Process.GetCurrentProcess();
            return proc.WorkingSet64 / 1024.0 / 1024.0;
        }

        public int GetProcessCount()
        {
            return Process.GetProcesses().Length;
        }
    }
}
