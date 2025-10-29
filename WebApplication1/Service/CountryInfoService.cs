using ServiceReference1;
using System.Collections.Concurrent;
using WebApplication1.Models;

namespace WebApplication1.Service
{
    public class CountryInfoService
    {
        private readonly CountryInfoServiceSoapTypeClient _client;
        private static ConcurrentDictionary<string, string> _countriesCache = new();

        public CountryInfoService()
        {
            _client = new CountryInfoServiceSoapTypeClient(
                CountryInfoServiceSoapTypeClient.EndpointConfiguration.CountryInfoServiceSoap12
            );
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

            await Task.WhenAll(capitalTask, currencyTask, phoneTask, flagTask);

            return new CountryInfoModel
            {
                IsoCode = isoCode,
                Name = name,
                CapitalCity = capitalTask.Result.Body.CapitalCityResult,
                Currency = $"{currencyTask.Result.Body.CountryCurrencyResult.sName} ({currencyTask.Result.Body.CountryCurrencyResult.sISOCode})",
                PhoneCode = "+" + phoneTask.Result.Body.CountryIntPhoneCodeResult,
                FlagUrl = flagTask.Result.Body.CountryFlagResult
            };
        }
    }
}
