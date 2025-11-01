using ServiceReference1;
using System.Collections.Concurrent;
using WebApplication1.Controllers;
using WebApplication1.Models;

namespace WebApplication1.Service
{
    public class CountryInfoService
    {
        private readonly CountryInfoServiceSoapTypeClient _client;
        private static ConcurrentDictionary<string, string> _countriesCache = new();
        private readonly ILogger<CountryInfoService> _logger;

        public CountryInfoService(ILogger<CountryInfoService> logger)
        {
            _client = new CountryInfoServiceSoapTypeClient(
                CountryInfoServiceSoapTypeClient.EndpointConfiguration.CountryInfoServiceSoap12
            );
            _logger = logger;
        }

        public async Task<IDictionary<string, string>> GetAllCountriesAsync()
        {
            if (_countriesCache.IsEmpty)
            {
                var result = await _client.ListOfCountryNamesByNameAsync();
                foreach (var c in result.Body.ListOfCountryNamesByNameResult)
                {
                    _countriesCache[c.sISOCode] = c.sName;
                }
            }
            return _countriesCache;
        }

        public async Task<CountryInfoModel> GetCountryDetailsAsync(string isoCode)
        {
            var countries = await GetAllCountriesAsync();
            var name = countries.ContainsKey(isoCode) ? countries[isoCode] : isoCode;

            var capitalTask = _client.CapitalCityAsync(isoCode);
            var currencyTask = _client.CountryCurrencyAsync(isoCode);
            var phoneTask = _client.CountryIntPhoneCodeAsync(isoCode);
            var flagTask = _client.CountryFlagAsync(isoCode);

            var flagUrl = flagTask.Result.Body.CountryFlagResult;

            // ********** ZDE JE NEJLEPŠÍ MÍSTO PRO OPRAVU **********
            if (!string.IsNullOrEmpty(flagUrl) && flagUrl.StartsWith("http://"))
            {
                // Převedeme na Protocol-Relative URL
                flagUrl = flagUrl.Replace("http://", "");
            }

            var isoLower = isoCode.ToLowerInvariant();
            var newFlagUrl = $"https://flagcdn.com/160x120/{isoLower}.png";

            await Task.WhenAll(capitalTask, currencyTask, phoneTask, flagTask);

            return new CountryInfoModel
            {
                IsoCode = isoCode,
                Name = name,
                CapitalCity = capitalTask.Result.Body.CapitalCityResult,
                Currency = $"{currencyTask.Result.Body.CountryCurrencyResult.sName} ({currencyTask.Result.Body.CountryCurrencyResult.sISOCode})",
                PhoneCode = "+" + phoneTask.Result.Body.CountryIntPhoneCodeResult,
                //FlagUrl = flagTask.Result.Body.CountryFlagResult
                FlagUrl = newFlagUrl
            };
        }
    }
}
