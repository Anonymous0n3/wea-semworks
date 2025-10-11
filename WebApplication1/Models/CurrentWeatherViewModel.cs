namespace WebApplication1.Models
{
    public class CurrentWeatherViewModel
    {
        public string LocationName { get; set; }
        public double TempC { get; set; }
        public double TempF { get; set; }
        public string ConditionText { get; set; }
        public string ConditionIcon { get; set; }
        public int Humidity { get; set; }
        public double WindKph { get; set; }
        public string WindDir { get; set; }
        public double DewPoint { get; set; }
        public double Uv { get; set; }
        public int? UsEpaIndex { get; set; }
    }
}
