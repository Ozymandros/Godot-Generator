#nullable enable

using GodotGenerator.Api.Abstractions;
using GodotGenerator.Api.Dtos;
using GodotGenerator.Wizard.Contracts;

namespace GodotGenerator.Mcp.Api.Services;

/// <summary>
/// Adapts wizard plugin tool calls to the existing API generation/configuration endpoints.
/// </summary>
public sealed class ApiWizardGenerationGateway(IGodotGeneratorApiService apiService) : IWizardGenerationGateway
{
    /// <summary>
    /// Generate code from the provided wizard request by calling the API generation endpoint.
    /// </summary>
    /// <param name="request">The wizard generation request containing the prompt and optional project name.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The generated code or an error message wrapped in a string.</returns>
    public Task<string> GenerateCodeAsync(WizardToolRequest request, CancellationToken cancellationToken = default) =>
        InvokeGenerationAsync(x => x.GenerateCodeAsync(ToGenerateRequest(request), cancellationToken), cancellationToken);

    /// <summary>
    /// Generate plain text from the provided wizard request by calling the API generation endpoint.
    /// </summary>
    /// <param name="request">The wizard generation request containing the prompt and optional project name.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The generated text or an error message wrapped in a string.</returns>
    public Task<string> GenerateTextAsync(WizardToolRequest request, CancellationToken cancellationToken = default) =>
        InvokeGenerationAsync(x => x.GenerateTextAsync(ToGenerateRequest(request), cancellationToken), cancellationToken);

    /// <summary>
    /// Create a Godot scene from the provided wizard request by calling the API endpoint.
    /// </summary>
    /// <param name="request">The wizard generation request containing the prompt and optional project name.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The created scene content or an error message wrapped in a string.</returns>
    public Task<string> CreateSceneAsync(WizardToolRequest request, CancellationToken cancellationToken = default) =>
        InvokeGenerationAsync(x => x.CreateSceneAsync(ToGenerateRequest(request), cancellationToken), cancellationToken);

    /// <summary>
    /// Generate Godot UI elements from the provided wizard request by calling the API endpoint.
    /// </summary>
    /// <param name="request">The wizard generation request containing the prompt and optional project name.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The generated UI content or an error message wrapped in a string.</returns>
    public Task<string> GenerateGodotUiAsync(WizardToolRequest request, CancellationToken cancellationToken = default) =>
        InvokeGenerationAsync(x => x.GenerateGodotUiAsync(ToGenerateRequest(request), cancellationToken), cancellationToken);

    /// <summary>
    /// Generate Godot physics configuration from the provided wizard request by calling the API endpoint.
    /// </summary>
    /// <param name="request">The wizard generation request containing the prompt and optional project name.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The generated physics content or an error message wrapped in a string.</returns>
    public Task<string> GenerateGodotPhysicsAsync(WizardToolRequest request, CancellationToken cancellationToken = default) =>
        InvokeGenerationAsync(x => x.GenerateGodotPhysicsAsync(ToGenerateRequest(request), cancellationToken), cancellationToken);

    /// <summary>
    /// Generate animation resources from the provided wizard request by calling the API endpoint.
    /// </summary>
    /// <param name="request">The wizard generation request containing the prompt and optional project name.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The generated animation content or an error message wrapped in a string.</returns>
    public Task<string> GenerateAnimationsAsync(WizardToolRequest request, CancellationToken cancellationToken = default) =>
        InvokeGenerationAsync(x => x.GenerateAnimationsAsync(ToGenerateRequest(request), cancellationToken), cancellationToken);

    /// <summary>
    /// Generate a Godot project scaffold from the provided wizard request by calling the API endpoint.
    /// </summary>
    /// <param name="request">The wizard generation request containing the prompt and optional project name.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The generated project content or an error message wrapped in a string.</returns>
    public Task<string> GenerateGodotProjectAsync(WizardToolRequest request, CancellationToken cancellationToken = default) =>
        InvokeGenerationAsync(x => x.GenerateGodotProjectAsync(ToGenerateRequest(request), cancellationToken), cancellationToken);

    /// <summary>
    /// Generate Godot lighting configuration from the provided wizard request by calling the API endpoint.
    /// </summary>
    /// <param name="request">The wizard generation request containing the prompt and optional project name.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The generated lighting content or an error message wrapped in a string.</returns>
    public Task<string> GenerateGodotLightingAsync(WizardToolRequest request, CancellationToken cancellationToken = default) =>
        InvokeGenerationAsync(x => x.GenerateGodotLightingAsync(ToGenerateRequest(request), cancellationToken), cancellationToken);

    /// <summary>
    /// Generate camera configuration for Godot from the provided wizard request by calling the API endpoint.
    /// </summary>
    /// <param name="request">The wizard generation request containing the prompt and optional project name.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The generated camera content or an error message wrapped in a string.</returns>
    public Task<string> GenerateGodotCameraAsync(WizardToolRequest request, CancellationToken cancellationToken = default) =>
        InvokeGenerationAsync(x => x.GenerateGodotCameraAsync(ToGenerateRequest(request), cancellationToken), cancellationToken);

    /// <summary>
    /// Generate Godot shader code from the provided wizard request by calling the API endpoint.
    /// </summary>
    /// <param name="request">The wizard generation request containing the prompt and optional project name.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The generated shader code or an error message wrapped in a string.</returns>
    public Task<string> GenerateGodotShadersAsync(WizardToolRequest request, CancellationToken cancellationToken = default) =>
        InvokeGenerationAsync(x => x.GenerateGodotShadersAsync(ToGenerateRequest(request), cancellationToken), cancellationToken);

    /// <summary>
    /// Generate Godot signal definitions from the provided wizard request by calling the API endpoint.
    /// </summary>
    /// <param name="request">The wizard generation request containing the prompt and optional project name.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The generated signals content or an error message wrapped in a string.</returns>
    public Task<string> GenerateGodotSignalsAsync(WizardToolRequest request, CancellationToken cancellationToken = default) =>
        InvokeGenerationAsync(x => x.GenerateGodotSignalsAsync(ToGenerateRequest(request), cancellationToken), cancellationToken);

    /// <summary>
    /// Generate Godot node definitions from the provided wizard request by calling the API endpoint.
    /// </summary>
    /// <param name="request">The wizard generation request containing the prompt and optional project name.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The generated nodes content or an error message wrapped in a string.</returns>
    public Task<string> GenerateGodotNodesAsync(WizardToolRequest request, CancellationToken cancellationToken = default) =>
        InvokeGenerationAsync(x => x.GenerateGodotNodesAsync(ToGenerateRequest(request), cancellationToken), cancellationToken);

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

        if (result.Data?.TryGetValue("godotToolNames", out var tools) == true && tools is System.Collections.IEnumerable toolEnum and not string)
        {
            var toolNames = toolEnum.Cast<object>().Select(static x => x.ToString()).Where(static x => !string.IsNullOrWhiteSpace(x)).ToArray();
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

    /// <summary>
    /// Executes a generation call against the legacy API service and normalizes the textual response.
    /// </summary>
    /// <param name="call">Generation call delegate against <see cref="IGodotGeneratorApiService"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Generated message text or formatted error content.</returns>
    private async Task<string> InvokeGenerationAsync(
        Func<IGodotGeneratorApiService, Task<ApiResponse<Dictionary<string, object?>>>> call,
        CancellationToken cancellationToken)
    {
        var result = await call(apiService).ConfigureAwait(false);
        if (!result.Success)
        {
            return $"[Generation failed: {result.Error ?? "unknown error"}]";
        }

        var message = result.Data?.TryGetValue("message", out var msg) == true
            ? msg?.ToString()
            : null;

        return string.IsNullOrWhiteSpace(message)
            ? "[Generation completed but returned no content.]"
            : message.Trim();
    }

    /// <summary>
    /// Converts a wizard tool payload into the shared API generation request contract.
    /// </summary>
    /// <param name="request">Wizard tool payload.</param>
    /// <returns>Normalized API generation request.</returns>
    private static GenerateRequest ToGenerateRequest(WizardToolRequest request) =>
        new(
            Prompt: request.Prompt.Trim(),
            ProjectName: string.IsNullOrWhiteSpace(request.ProjectName) ? null : request.ProjectName.Trim());
}
