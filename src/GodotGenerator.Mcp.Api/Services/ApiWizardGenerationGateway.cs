#nullable enable

using GodotGenerator.Api.Abstractions;
using GodotGenerator.Api.Dtos;
using GodotGenerator.Wizard.Contracts;

namespace GodotGenerator.Mcp.Api.Services;

/// <summary>
/// Adapts wizard plugin utility tool calls to the existing API configuration endpoints.
/// </summary>
/// <remarks>
/// Generation endpoints are no longer mediated through this gateway. The Wizard kernel
/// now hosts dedicated generation plugins (GodotMcp, ElevenLabs, ImageGen) that the
/// LLM orchestrates directly. This class handles only configuration inspection and
/// preference management on behalf of <see cref="GodotGenerator.Plugins.Plugins.WizardMcpPlugin"/>.
/// </remarks>
public sealed class ApiWizardGenerationGateway(IGodotGeneratorApiService apiService) : IWizardGenerationGateway
{
    /// <summary>
    /// Retrieve a human-readable summary of the current generator configuration from the API.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A multi-line string summarizing configuration highlights or an error message.</returns>
    public async Task<string> GetConfigurationAsync(CancellationToken cancellationToken = default)
    {
        var result = await apiService.GetAllConfigAsync(cancellationToken).ConfigureAwait(false);
        if (!result.Success)
        {
            return $"[Error retrieving configuration: {result.Error}]";
        }

        var lines = new List<string>();

        if (result.Data?.TryGetValue("defaultLlmProvider", out var provider) == true && provider is not null)
        {
            lines.Add($"Default provider: {provider}");
        }

        if (result.Data?.TryGetValue("defaultChatModelId", out var model) == true && model is not null)
        {
            lines.Add($"Default model: {model}");
        }

        if (result.Data?.TryGetValue("godotToolNames", out var tools) == true
            && tools is System.Collections.IEnumerable toolEnum and not string)
        {
            var toolNames = toolEnum
                .Cast<object>()
                .Select(static x => x.ToString())
                .Where(static x => !string.IsNullOrWhiteSpace(x))
                .ToArray();

            if (toolNames.Length > 0)
            {
                lines.Add($"Godot MCP tools available: {string.Join(", ", toolNames)}");
            }
        }

        return lines.Count == 0
            ? "Configuration retrieved (no highlights to report)."
            : string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// Set a named preference in the generator configuration via the API.
    /// </summary>
    /// <param name="key">The preference key to set. Must not be empty or whitespace.</param>
    /// <param name="value">The value to set for the preference; pass null to clear the preference.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A success message or an error message wrapped in a string.</returns>
    public async Task<string> SetPreferenceAsync(string key, string? value, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return "[Error: preference key must not be empty.]";
        }

        var result = await apiService
            .SetPreferenceAsync(new SetPreferenceRequest(key.Trim(), value), cancellationToken)
            .ConfigureAwait(false);

        return result.Success
            ? $"Preference '{key}' set to '{value ?? "(cleared)"}'."
            : $"[Error setting preference '{key}': {result.Error}]";
    }
}
