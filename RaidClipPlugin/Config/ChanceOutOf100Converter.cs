using System.ComponentModel;
using System.Globalization;

namespace RaidClipPlugin.Config;

public sealed class ChanceOutOf100Converter : Int32Converter
{
    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType) =>
        destinationType == typeof(string) && value is int chance
            ? $"{chance}/100" : base.ConvertTo(context, culture, value, destinationType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is not string text) return base.ConvertFrom(context, culture, value);
        var parts = text.Trim().Split('/');
        if (parts.Length > 2 || (parts.Length == 2 && parts[1].Trim() != "100") ||
            !int.TryParse(parts[0].Trim(), NumberStyles.Integer, culture ?? CultureInfo.CurrentCulture, out var chance) || chance is < 0 or > 100)
            throw new FormatException("Bitte eine Chance von 0/100 bis 100/100 eingeben, z. B. 33/100 oder 33.");
        return chance;
    }
}
