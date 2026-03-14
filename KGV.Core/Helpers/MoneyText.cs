using System.Globalization;

namespace KGV.Core.Helpers;

public static class MoneyText
{
    private static readonly CultureInfo DeCulture = CultureInfo.GetCultureInfo("de-DE");

    public static string FormatEuro(decimal amount)
    {
        if (amount < 0m)
            amount = 0m;

        amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);

        if (amount == decimal.Truncate(amount))
            return $"{amount.ToString("0", DeCulture)},-€";

        return $"{amount.ToString("0.00", DeCulture)}€";
    }
}
