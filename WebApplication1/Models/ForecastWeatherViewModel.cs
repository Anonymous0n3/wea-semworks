namespace WebApplication1.Models
{
    public class ForecastWeatherViewModel
    {
        public string LocationName { get; set; }
        public List<ForecastDayViewModel> ForecastDays { get; set; } = new List<ForecastDayViewModel>();
    }

    public class ForecastDayViewModel
    {
        public string Date { get; set; }
        public string ConditionText { get; set; }
        public string ConditionIcon { get; set; }
        public double MaxTempC { get; set; }
        public double MinTempC { get; set; }
        public double ChanceOfRain { get; set; }
        public double Uv { get; set; }
        public int? UsEpaIndex { get; set; }
    }
}
