using System.Globalization;

namespace GodotGenerator.Blazor.Client.Services;

/// <summary>
/// Represents a supported UI culture/language for localization.
/// </summary>
public record UiCulture(string Code, string DisplayName, string NativeName);

/// <summary>
/// Service for managing UI culture and localization in the Blazor application.
/// Provides a list of supported cultures sorted alphabetically and handles culture changes.
/// </summary>
public class LocalizationService
{
    // Supported UI cultures - always sorted alphabetically by DisplayName
    private static readonly List<UiCulture> SupportedCultures = new()
    {
        new UiCulture("ca", "Catalan", "Català"),
        new UiCulture("zh", "Chinese", "中文"),
        new UiCulture("en", "English", "English"),
        new UiCulture("eu", "Euzkera", "Euskara"),
        new UiCulture("fa", "Farsi", "فارسی"),
        new UiCulture("fi", "Finnish", "Suomi"),
        new UiCulture("fr", "French", "Français"),
        new UiCulture("gl", "Galego", "Galego"),
        new UiCulture("de", "German", "Deutsch"),
        new UiCulture("hi", "Hindi", "हिन्दी"),
        new UiCulture("id", "Indonesian", "Bahasa Indonesia"),
        new UiCulture("it", "Italian", "Italiano"),
        new UiCulture("ja", "Japanese", "日本語"),
        new UiCulture("ko", "Korean", "한국어"),
        new UiCulture("oc", "Occitan (Aranès)", "Occitan"),
        new UiCulture("ps", "Pashtu", "پښتو"),
        new UiCulture("pl", "Polish", "Polski"),
        new UiCulture("pt", "Portuguese", "Português"),
        new UiCulture("es", "Spanish", "Español"),
        new UiCulture("th", "Thai", "ไทย"),
        new UiCulture("tr", "Turkish", "Türkçe"),
        new UiCulture("uk", "Ukrainian", "Українська"),
        new UiCulture("ur", "Urdu", "اردو"),
    };

    /// <summary>
    /// Gets all supported cultures sorted alphabetically by display name.
    /// </summary>
    public IReadOnlyList<UiCulture> GetSupportedCultures()
    {
        return SupportedCultures.OrderBy(c => c.DisplayName).ToList().AsReadOnly();
    }

    /// <summary>
    /// Sets the current UI culture for the application.
    /// </summary>
    /// <param name="cultureCode">The culture code (e.g., "en", "es", "fr").</param>
    public void SetCulture(string cultureCode)
    {
        if (string.IsNullOrWhiteSpace(cultureCode))
        {
            return;
        }

        var culture = SupportedCultures.FirstOrDefault(c => c.Code.Equals(cultureCode, StringComparison.OrdinalIgnoreCase));
        if (culture != null)
        {
            var cultureInfo = new CultureInfo(culture.Code);
            CultureInfo.CurrentCulture = cultureInfo;
            CultureInfo.CurrentUICulture = cultureInfo;
            
            // Trigger event for UI updates
            OnCultureChanged?.Invoke(this, culture);
        }
    }

    /// <summary>
    /// Gets the current UI culture code.
    /// </summary>
    public string GetCurrentCultureCode()
    {
        return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
    }

    /// <summary>
    /// Event fired when the culture changes.
    /// </summary>
    public event EventHandler<UiCulture>? OnCultureChanged;

    /// <summary>
    /// Gets a culture by its code.
    /// </summary>
    public UiCulture? GetCultureByCode(string code)
    {
        return SupportedCultures.FirstOrDefault(c => c.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
    }
}
