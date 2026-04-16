#nullable enable

using System.Threading;
using System.Threading.Tasks;

namespace GodotGenerator.Wizard.Contracts;

/// <summary>
/// Normalized request payload for wizard-initiated generation tool calls.
/// </summary>
/// <param name="Prompt">Tool-specific prompt to execute.</param>
/// <param name="ProjectName">Optional project label propagated from the wizard context.</param>
public sealed record WizardToolRequest(
    string Prompt,
    string? ProjectName = null);

/// <summary>
/// Contract implemented by API-facing adapters that bridge wizard plugin tools
/// to existing generation/configuration endpoints.
/// </summary>
public interface IWizardGenerationGateway
{
    /// <summary>Calls the legacy code generation endpoint.</summary>
    /// <param name="request">Wizard-normalized generation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Generated content text or a formatted error string.</returns>
    Task<string> GenerateCodeAsync(WizardToolRequest request, CancellationToken cancellationToken = default);

    /// <summary>Calls the legacy text generation endpoint.</summary>
    /// <param name="request">Wizard-normalized generation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Generated content text or a formatted error string.</returns>
    Task<string> GenerateTextAsync(WizardToolRequest request, CancellationToken cancellationToken = default);

    /// <summary>Calls the legacy scene creation endpoint.</summary>
    /// <param name="request">Wizard-normalized generation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Generated content text or a formatted error string.</returns>
    Task<string> CreateSceneAsync(WizardToolRequest request, CancellationToken cancellationToken = default);

    /// <summary>Calls the legacy Godot UI generation endpoint.</summary>
    /// <param name="request">Wizard-normalized generation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Generated content text or a formatted error string.</returns>
    Task<string> GenerateGodotUiAsync(WizardToolRequest request, CancellationToken cancellationToken = default);

    /// <summary>Calls the legacy Godot physics generation endpoint.</summary>
    /// <param name="request">Wizard-normalized generation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Generated content text or a formatted error string.</returns>
    Task<string> GenerateGodotPhysicsAsync(WizardToolRequest request, CancellationToken cancellationToken = default);

    /// <summary>Calls the legacy animation generation endpoint.</summary>
    /// <param name="request">Wizard-normalized generation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Generated content text or a formatted error string.</returns>
    Task<string> GenerateAnimationsAsync(WizardToolRequest request, CancellationToken cancellationToken = default);

    /// <summary>Calls the legacy Godot project generation endpoint.</summary>
    /// <param name="request">Wizard-normalized generation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Generated content text or a formatted error string.</returns>
    Task<string> GenerateGodotProjectAsync(WizardToolRequest request, CancellationToken cancellationToken = default);

    /// <summary>Calls the legacy Godot lighting generation endpoint.</summary>
    /// <param name="request">Wizard-normalized generation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Generated content text or a formatted error string.</returns>
    Task<string> GenerateGodotLightingAsync(WizardToolRequest request, CancellationToken cancellationToken = default);

    /// <summary>Calls the legacy Godot camera generation endpoint.</summary>
    /// <param name="request">Wizard-normalized generation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Generated content text or a formatted error string.</returns>
    Task<string> GenerateGodotCameraAsync(WizardToolRequest request, CancellationToken cancellationToken = default);

    /// <summary>Calls the legacy Godot shader generation endpoint.</summary>
    /// <param name="request">Wizard-normalized generation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Generated content text or a formatted error string.</returns>
    Task<string> GenerateGodotShadersAsync(WizardToolRequest request, CancellationToken cancellationToken = default);

    /// <summary>Calls the legacy Godot signals generation endpoint.</summary>
    /// <param name="request">Wizard-normalized generation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Generated content text or a formatted error string.</returns>
    Task<string> GenerateGodotSignalsAsync(WizardToolRequest request, CancellationToken cancellationToken = default);

    /// <summary>Calls the legacy Godot nodes generation endpoint.</summary>
    /// <param name="request">Wizard-normalized generation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Generated content text or a formatted error string.</returns>
    Task<string> GenerateGodotNodesAsync(WizardToolRequest request, CancellationToken cancellationToken = default);

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
