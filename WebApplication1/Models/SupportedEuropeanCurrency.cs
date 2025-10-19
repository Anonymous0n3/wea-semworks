namespace WebApplication1.Models
{
    public enum SupportedEuropeanCurrency
    {
        GBP, ALL, AMD, AZN, BYN, BAM, BGN, CZK, DKK, GEL, HUF, ISK,
        CHF, MDL, MKD, NOK, PLN, RON, RUB, RSD, SEK, TRY, UAH
    }

    public static class SupportedEuropeanCurrencyHelper
    {
        public static IReadOnlyList<string> ToIsoList() =>
            Enum.GetNames(typeof(SupportedEuropeanCurrency)).ToList();
    }
}
