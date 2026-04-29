#nullable enable

namespace GodotGenerator.Wizard.Contracts;

/// <summary>
/// Contract implemented by API-facing adapters that bridge wizard plugin utility tools
/// to configuration/preference endpoints.
/// </summary>
/// <remarks>
/// Generation work is no longer delegated through this interface. The Wizard kernel
/// now hosts dedicated generation plugins directly (GodotMcp, ElevenLabs, ImageGen)
/// alongside this utility plugin, and the LLM orchestrates them autonomously.
/// </remarks>
public interface IWizardGenerationGateway
{
    /// <summary>Returns a concise snapshot of runtime configuration.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Human-readable configuration summary or formatted error text.</returns>
    Task<string> GetConfigurationAsync(CancellationToken cancellationToken = default);

    /// <summary>Sets a runtime preference key/value pair.</summary>
    /// <param name="key">Preference key.</param>
    /// <param name="value">Preference value; null clears value where supported.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Human-readable result text.</returns>
    Task<string> SetPreferenceAsync(string key, string? value, CancellationToken cancellationToken = default);
}
