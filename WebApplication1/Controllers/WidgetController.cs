using Microsoft.AspNetCore.Mvc;
using WidgetsDemo.Models;
using WidgetsDemo.Services;

namespace WidgetsDemo.Controllers
{
    public class WidgetController : Controller
    {
        private readonly SystemMetricsService _metricsService;

        public WidgetController(SystemMetricsService metricsService)
        {
            _metricsService = metricsService;
        }

        public IActionResult AppInfo()
        {
            var model = new AppInfoModel
            {
                CpuUsage = _metricsService.GetCpuUsage(),
                MemoryUsageMB = _metricsService.GetMemoryUsageMB(),
                ProcessCount = _metricsService.GetProcessCount(),
            };
            return PartialView("~/Views/Widgets/_AppInfoWidget.cshtml", model);
        }
    }
}
