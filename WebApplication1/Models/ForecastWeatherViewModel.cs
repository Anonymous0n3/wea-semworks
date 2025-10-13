namespace WebApplication1.Models
{
    public class ForecastWeatherViewModel
    {
        public string LocationName { get; set; } = "";
        public List<ForecastDayViewModel> ForecastDays { get; set; } = new();
    }

    public class ForecastDayViewModel
    {
        public string Date { get; set; } = "";
        public string ConditionText { get; set; } = "";
        public string ConditionIcon { get; set; } = "";
        public double MaxTempC { get; set; }
        public double MinTempC { get; set; }
        public int ChanceOfRain { get; set; }
        public double Uv { get; set; }
        public int? UsEpaIndex { get; set; }

        public List<HourlyForecastViewModel> Hourly { get; set; } = new();
    }

    public class HourlyForecastViewModel
    {
        public string Hour { get; set; } = "";
        public double TempC { get; set; }
        public double TempF { get; set; }
    }
}
