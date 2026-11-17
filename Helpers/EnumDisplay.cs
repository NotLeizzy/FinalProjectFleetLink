using System.Text.RegularExpressions;

namespace FinalProjectFleetLink.Helpers;

public sealed record EnumOption<TEnum>(TEnum Value, string DisplayName)
    where TEnum : struct, Enum;

public static partial class EnumDisplay
{
    private static readonly Regex AcronymBoundary = new("([A-Z]+)([A-Z][a-z])");
    private static readonly Regex WordBoundary = new("([a-z0-9])([A-Z])");

    public static string ToDisplayName<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        return ToDisplayName((Enum)(object)value);
    }

    public static string ToDisplayName(Enum value)
    {
        var name = value.ToString();
        name = AcronymBoundary.Replace(name, "$1 $2");
        name = WordBoundary.Replace(name, "$1 $2");
        return name;
    }

    public static void FillComboBox<TEnum>(ComboBox comboBox)
        where TEnum : struct, Enum
    {
        ArgumentNullException.ThrowIfNull(comboBox);
        var options = Enum.GetValues<TEnum>()
            .Select(value => new EnumOption<TEnum>(value, ToDisplayName(value)))
            .ToList();
        comboBox.DataSource = options;
        comboBox.DisplayMember = nameof(EnumOption<TEnum>.DisplayName);
        comboBox.ValueMember = nameof(EnumOption<TEnum>.Value);
    }

}
