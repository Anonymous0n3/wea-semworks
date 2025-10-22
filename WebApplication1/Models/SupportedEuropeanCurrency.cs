namespace WebApplication1.Models
{
    public enum SupportedEuropeanCurrency
    {
        EUR, BGN, CZK, DKK, HUF, PLN, RON, SEK, CHF
    }

    public static class SupportedEuropeanCurrencyHelper
    {
        public static IReadOnlyList<string> ToIsoList() =>
            Enum.GetNames(typeof(SupportedEuropeanCurrency)).ToList();
    }
}
