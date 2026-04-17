#nullable enable

using System.ComponentModel;
using GodotGenerator.Wizard.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;

namespace GodotGenerator.Plugins.Plugins;

/// <summary>
/// Semantic Kernel utility plugin for the Wizard kernel.
/// Exposes configuration inspection and preference management as LLM-callable tools.
/// </summary>
/// <remarks>
/// Generation work is handled directly by the co-registered specialist plugins
/// (GodotMcp, ElevenLabs, ImageGen). This plugin's role is limited to utility
/// operations that require access to the app's configuration layer.
/// </remarks>
public sealed class WizardMcpPlugin(IServiceScopeFactory scopeFactory)
{
    /// <summary>Returns a configuration summary through the wizard gateway.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Human-readable configuration summary or formatted error text.</returns>
    [KernelFunction("get_configuration")]
    [Description("Returns a concise summary of current app configuration: active LLM provider, model, and available Godot MCP tools.")]
    public Task<string> GetConfigurationAsync(CancellationToken cancellationToken = default) =>
        InvokeAsync(x => x.GetConfigurationAsync(cancellationToken));

    /// <summary>Sets a preference through the wizard gateway.</summary>
    /// <param name="key">Preference key.</param>
    /// <param name="value">Preference value.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Human-readable success/error text.</returns>
    [KernelFunction("set_preference")]
    [Description("Sets an application preference key-value pair, e.g. preferred_llm_provider or preferred_llm_model.")]
    public Task<string> SetPreferenceAsync(
        [Description("Preference key, e.g. preferred_llm_provider.")] string key,
        [Description("Preference value. Null or empty string clears the preference.")] string? value,
        CancellationToken cancellationToken = default) =>
        InvokeAsync(x => x.SetPreferenceAsync(key, value, cancellationToken));

    /// <summary>
    /// Resolves the scoped wizard gateway and executes a plugin tool delegate safely.
    /// Exceptions other than <see cref="OperationCanceledException"/> are caught and
    /// returned as formatted error strings so the LLM can reason about failures.
    /// </summary>
    /// <param name="call">Gateway delegate for the current tool call.</param>
    /// <returns>Tool result text or formatted error content.</returns>
    private async Task<string> InvokeAsync(
        Func<IWizardGenerationGateway, Task<string>> call)
    {
        using var scope = scopeFactory.CreateScope();
        var gateway = scope.ServiceProvider.GetRequiredService<IWizardGenerationGateway>();
        try
        {
            return await call(gateway).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return $"[Tool call error: {ex.Message}]";
        }
    }
}
