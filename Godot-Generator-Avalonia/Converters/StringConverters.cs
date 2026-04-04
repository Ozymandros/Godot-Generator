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

    /// <summary>True when value is non-null (for visibility of detail panels).</summary>
    public static IValueConverter IsNotNull { get; } = new NotNullObjectToBoolConverter();

    public static IValueConverter ToUpper { get; } = new ToUpperConverter();

    private sealed class ToUpperConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value?.ToString()?.ToUpperInvariant() ?? string.Empty;

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    public static IValueConverter ModalityToBrush { get; } = new ModalityToBrushConverter();

    private sealed class ModalityToBrushConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            var modality = value?.ToString()?.ToLowerInvariant();
            return modality switch
            {
                "llm" => Avalonia.Media.Brush.Parse("#2196F3"), // Blue
                "image" => Avalonia.Media.Brush.Parse("#9C27B0"), // Purple
                "audio" => Avalonia.Media.Brush.Parse("#FF9800"), // Orange
                "video" => Avalonia.Media.Brush.Parse("#F44336"), // Red
                "sprites" => Avalonia.Media.Brush.Parse("#4CAF50"), // Green
                "godot-ui" or "godot_ui" => Avalonia.Media.Brush.Parse("#00BCD4"), // Cyan
                "godot-physics" or "godot_physics" => Avalonia.Media.Brush.Parse("#607D8B"), // Blue Grey
                _ => Avalonia.Media.Brush.Parse("#757575"), // Grey
            };
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
            throw new NotSupportedException();
    }

    private sealed class NotNullObjectToBoolConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not null;

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

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

