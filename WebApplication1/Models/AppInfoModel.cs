namespace WidgetsDemo.Models
{
    public class AppInfoModel
    {
        public string AppVersion { get; set; } = "1.0.0";
        public DateTime LastDeployment { get; set; } = DateTime.UtcNow.AddDays(-1);
        public double CpuUsage { get; set; }
        public double MemoryUsageMB { get; set; }
        public int ProcessCount { get; set; }
    }
}
