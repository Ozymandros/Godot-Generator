using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Godot_Generator_Avalonia.Converters;

/// <summary>
/// Common string helpers as value converters for XAML.
/// </summary>
public static class StringConverters
{
    public static IValueConverter IsNotNullOrEmpty { get; } = new NotNullOrEmptyStringToBoolConverter();

    private sealed class NotNullOrEmptyStringToBoolConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is null)
            {
                return false;
            }

            if (value is string s)
            {
                return !string.IsNullOrWhiteSpace(s);
            }

            // Fallback: treat non-empty ToString() as true.
            var str = value.ToString();
            return !string.IsNullOrWhiteSpace(str);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}

