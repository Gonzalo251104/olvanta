using System.Globalization;

namespace Olvanta.Api.Billing;

internal static class FiscalLegend
{
    private static readonly string[] Units = ["cero", "un", "dos", "tres", "cuatro", "cinco", "seis", "siete", "ocho", "nueve",
        "diez", "once", "doce", "trece", "catorce", "quince", "dieciséis", "diecisiete", "dieciocho", "diecinueve",
        "veinte", "veintiún", "veintidós", "veintitrés", "veinticuatro", "veinticinco", "veintiséis", "veintisiete", "veintiocho", "veintinueve"];
    private static readonly string[] Tens = ["", "", "", "treinta", "cuarenta", "cincuenta", "sesenta", "setenta", "ochenta", "noventa"];
    private static readonly string[] Hundreds = ["", "ciento", "doscientos", "trescientos", "cuatrocientos", "quinientos", "seiscientos", "setecientos", "ochocientos", "novecientos"];

    public static string Total(decimal amount, string currency)
    {
        var whole = decimal.Truncate(amount);
        string denomination = currency switch
        {
            "PEN" => whole == 1 ? "sol" : "soles",
            "USD" => whole == 1 ? "dólar estadounidense" : "dólares estadounidenses",
            _ => throw new ArgumentOutOfRangeException(nameof(currency))
        };
        string cents = ((amount - whole) * 100).ToString("00", CultureInfo.InvariantCulture);
        return $"{Words(whole)} con {cents}/100 {denomination}".ToUpperInvariant();
    }

    private static string Words(decimal n)
    {
        if (n < 30) return Units[(int)n];
        if (n < 100) return Tens[(int)(n / 10)] + (n % 10 == 0 ? "" : " y " + Words(n % 10));
        if (n == 100) return "cien";
        if (n < 1000) return Hundreds[(int)(n / 100)] + Remainder(n % 100);
        if (n < 1000000) return (n < 2000 ? "mil" : Words(decimal.Truncate(n / 1000)) + " mil") + Remainder(n % 1000);
        var scale = n < 1000000000000m ? 1000000m : 1000000000000m;
        var count = decimal.Truncate(n / scale);
        var group = scale == 1000000 ? (count == 1 ? "millón" : "millones") : (count == 1 ? "billón" : "billones");
        return Words(count) + " " + group + Remainder(n % scale);
    }

    private static string Remainder(decimal value) => value == 0 ? "" : " " + Words(value);
}
