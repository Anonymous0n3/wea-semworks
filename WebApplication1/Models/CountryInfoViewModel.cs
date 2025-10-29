namespace WebApplication1.Models
{
    public class CountryInfoViewModel
    {
        public IDictionary<string, string> Countries { get; set; } = new Dictionary<string, string>();
        public CountryInfoModel SelectedCountry { get; set; } = null;
    }
}
