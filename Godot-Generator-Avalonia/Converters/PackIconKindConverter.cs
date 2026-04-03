using Avalonia.Data.Converters;
using System.Globalization;

namespace Godot_Generator_Avalonia.Converters;

public sealed class PackIconKindConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s && !string.IsNullOrWhiteSpace(s))
        {
            // Try to resolve PackIconKind enum type from loaded assemblies
            var enumType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("Material.Avalonia.PackIconKind") ?? a.GetType("Material.Avalonia.Controls.PackIconKind"))
                .FirstOrDefault(t => t != null);

            if (enumType is Type et)
            {
                if (Enum.TryParse(et, s, true, out var parsed))
                {
                    return parsed!;
                }

                var values = Enum.GetValues(et);
                return values.Length > 0 ? values.GetValue(0)! : 0;
            }

            // If enum type not found, just return the string — the binding will likely fail gracefully.
            return s;
        }

        return string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
