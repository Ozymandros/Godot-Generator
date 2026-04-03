using Avalonia.Data.Converters;
using System;
using System.Globalization;
using System.Linq;

namespace Godot_Generator_Avalonia.Converters
{
    public class MaterialIconNameToEnumConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not string key) return value ?? string.Empty;

            // Try to find the MaterialIconKind enum type from loaded assemblies
            var enumType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("Material.Icons.Avalonia.MaterialIconKind") ?? a.GetType("Material.Icons.Avalonia.MaterialIconsKind") )
                .FirstOrDefault(t => t != null);

            if (enumType is null)
                return value; // cannot convert, return original

            // Explicit common mappings to handle differences between friendly names and enum members
            var explicitMap = new System.Collections.Generic.Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                { "text", new[] { "FileDocumentOutline", "TextBox", "Text" } },
                { "code", new[] { "Xml", "CodeBraces", "Code" } },
                { "image", new[] { "ImageOutline", "Image", "ImageOutlined" } },
                { "audio", new[] { "VolumeHigh", "MusicNote", "Music" } },
                { "video", new[] { "MovieOutline", "Movie" } },
                { "sprites", new[] { "Grid", "GridView", "GridOn" } },
                { "godot_ui", new[] { "PaletteOutline", "Widgets" } },
                { "godot_physics", new[] { "Atom", "ScrewMachineFlatHead" } },
                { "scenes", new[] { "ViewDashboardOutline", "ViewDashboard" } },
                { "godot_project", new[] { "Unity", "Projector" } },
                { "config", new[] { "CogOutline", "Settings", "Cog" } },
                { "settings", new[] { "CogOutline", "Settings", "SettingsOutline", "Cog" } },
                { "cog", new[] { "CogOutline", "Settings", "Cog", "Gear" } },
                { "providers", new[] { "RobotOutline", "Robot" } },
                { "models", new[] { "RobotOutline", "Robot" } },
                { "prompts", new[] { "ScriptTextOutline", "ScriptText" } },
                { "secrets", new[] { "KeyChain", "ShieldKey" } },
                { "text_short", new[] { "TextShort", "CommentTextOutline" } },
                { "code_braces", new[] { "CodeBraces", "Code" } },
                { "screw_machine_flat_head", new[] { "ScrewMachineFlatHead", "Wrench", "Tools" } },
            };
            string keyStr = key ?? string.Empty;
            if (explicitMap.TryGetValue(keyStr, out var candidates))
            {
                foreach (var cand in candidates)
                {
                    if (Enum.TryParse(enumType, cand, true, out var parsedCand))
                        return parsedCand!;
                }
            }

            // Try to match by normalizing names (remove non-alphanumeric, case-insensitive)
            string Normalize(string? s) => new string((s ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
            var target = Normalize(key!);

            var names = Enum.GetNames(enumType);
            // Exact normalized match first
            foreach (var name in names)
            {
                if (Normalize(name) == target)
                {
                    if (Enum.TryParse(enumType, name, true, out var parsedMatch))
                        return parsedMatch!;
                }
            }

            // Contains match: enum name contains the target or vice versa (helps with "text" vs "textfields")
            foreach (var name in names)
            {
                var n = Normalize(name);
                if (n.Contains(target) || target.Contains(n))
                {
                    if (Enum.TryParse(enumType, name, true, out var parsedMatch))
                        return parsedMatch!;
                }
            }

            // Try common PascalCase candidate derived from underscores/spaces
            var candidate = keyStr.Replace("_", " ");
            candidate = string.Join(string.Empty, candidate.Split(' ').Select(s => char.ToUpperInvariant(s[0]) + (s.Length > 1 ? s.Substring(1) : string.Empty)));
            if (Enum.TryParse(enumType, candidate, true, out var parsed))
                return parsed!;

            // Fallback: try direct parse of original key
            if (Enum.TryParse(enumType, key, true, out parsed))
                return parsed!;

            return value;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
