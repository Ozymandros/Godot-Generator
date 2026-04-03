using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace Godot_Generator_Avalonia.Converters
{
    public class MaterialIconKeyToGlyphConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not string key)
                return string.Empty;

            return key switch
            {
                "settings" => "⚙",
                "text_fields" => "📝",
                "code" => "</>",
                "image" => "🖼",
                "music_note" => "🎵",
                "movie" => "🎬",
                "grid_on" => "▦",
                "widgets" => "▦",
                "build" => "🔧",
                _ => "•",
            };
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
