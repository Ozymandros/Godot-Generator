namespace GodotGenerator.Blazor.Components.Layout;

/// <summary>
/// Centralised UI theme constants for the Blazor shell.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="AccentColor"/> is the single source of truth for the application
/// accent colour. It must remain in sync with the <c>--app-color-accent</c> CSS
/// custom property declared in <c>wwwroot/app.css</c>.
/// </para>
/// <para>
/// <b>Why a C# constant?</b><br/>
/// <see cref="Microsoft.FluentUI.AspNetCore.Components.FluentDesignTheme"/> accepts
/// <c>CustomColor</c> as a raw hex string — it cannot resolve CSS custom properties
/// at component-initialisation time. Keeping the hex here (rather than duplicating it
/// in Razor markup) means a colour update requires changing exactly one file.
/// </para>
/// </remarks>
public static class AppTheme
{
    /// <summary>
    /// Primary accent colour used by <c>FluentDesignTheme</c> and as the value of
    /// the <c>--app-color-accent</c> CSS token.
    /// </summary>
    /// <remarks>
    /// Update this value in tandem with <c>--app-color-accent</c>,
    /// <c>--app-color-accent-hover</c>, <c>--app-color-accent-dark</c>, and
    /// <c>--app-color-accent-light</c> in <c>wwwroot/app.css</c> whenever the
    /// brand accent changes.
    /// </remarks>
    public const string AccentColor = "#5b8fd4";
}
