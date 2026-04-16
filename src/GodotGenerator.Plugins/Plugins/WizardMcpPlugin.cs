#nullable enable

using System.ComponentModel;
using GodotGenerator.Wizard.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;

namespace GodotGenerator.Plugins.Plugins;

/// <summary>
/// Semantic Kernel plugin that exposes generation/configuration tools for wizard turns.
/// Tool calls are delegated to API adapters via <see cref="IWizardGenerationGateway"/>.
/// </summary>
public sealed class WizardMcpPlugin(IServiceScopeFactory scopeFactory)
{
    /// <summary>Generates Godot code through the wizard gateway.</summary>
    /// <param name="prompt">Tool prompt describing desired code output.</param>
    /// <param name="projectName">Optional project context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Generated content or formatted tool error text.</returns>
    [KernelFunction("generate_code")]
    [Description("Generates GDScript or C# code for Godot 4.")]
    public Task<string> GenerateCodeAsync(
        [Description("Describe the code to generate.")] string prompt,
        [Description("Optional Godot project name for context.")] string? projectName = null,
        CancellationToken cancellationToken = default) =>
        InvokeAsync(x => x.GenerateCodeAsync(new WizardToolRequest(prompt, projectName), cancellationToken));

    /// <summary>Generates narrative/text assets through the wizard gateway.</summary>
    /// <param name="prompt">Tool prompt describing desired text output.</param>
    /// <param name="projectName">Optional project context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Generated content or formatted tool error text.</returns>
    [KernelFunction("generate_text")]
    [Description("Generates in-game text content for Godot projects.")]
    public Task<string> GenerateTextAsync(
        [Description("Describe the text content needed.")] string prompt,
        [Description("Optional Godot project name for context.")] string? projectName = null,
        CancellationToken cancellationToken = default) =>
        InvokeAsync(x => x.GenerateTextAsync(new WizardToolRequest(prompt, projectName), cancellationToken));

    /// <summary>Generates or updates a Godot scene through the wizard gateway.</summary>
    /// <param name="prompt">Tool prompt describing the scene.</param>
    /// <param name="scenePath">Optional scene path hint.</param>
    /// <param name="projectName">Optional project context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Generated content or formatted tool error text.</returns>
    [KernelFunction("create_scene")]
    [Description("Designs or creates a Godot scene (.tscn) structure.")]
    public Task<string> CreateSceneAsync(
        [Description("Describe the scene's purpose and node structure.")] string prompt,
        [Description("Optional target scene path, e.g. res://scenes/main.tscn.")] string? scenePath = null,
        [Description("Optional Godot project name for context.")] string? projectName = null,
        CancellationToken cancellationToken = default)
    {
        var combined = string.IsNullOrWhiteSpace(scenePath)
            ? prompt
            : $"Scene file hint: {scenePath.Trim()}{Environment.NewLine}{Environment.NewLine}{prompt}";
        return InvokeAsync(x => x.CreateSceneAsync(new WizardToolRequest(combined, projectName), cancellationToken));
    }

    /// <summary>Generates Godot UI assets through the wizard gateway.</summary>
    /// <param name="prompt">Tool prompt describing desired UI output.</param>
    /// <param name="projectName">Optional project context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Generated content or formatted tool error text.</returns>
    [KernelFunction("generate_godot_ui")]
    [Description("Generates Godot UI layouts, containers, and themes.")]
    public Task<string> GenerateGodotUiAsync(
        [Description("Describe the UI layout or component needed.")] string prompt,
        [Description("Optional Godot project name for context.")] string? projectName = null,
        CancellationToken cancellationToken = default) =>
        InvokeAsync(x => x.GenerateGodotUiAsync(new WizardToolRequest(prompt, projectName), cancellationToken));

    /// <summary>Generates Godot physics configuration through the wizard gateway.</summary>
    /// <param name="prompt">Tool prompt describing desired physics output.</param>
    /// <param name="physicsContext">Optional physics constraints/context.</param>
    /// <param name="projectName">Optional project context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Generated content or formatted tool error text.</returns>
    [KernelFunction("generate_godot_physics")]
    [Description("Generates Godot physics setup: bodies, collisions, and movement logic.")]
    public Task<string> GenerateGodotPhysicsAsync(
        [Description("Describe the physics behavior or setup.")] string prompt,
        [Description("Optional collision and layer notes.")] string? physicsContext = null,
        [Description("Optional Godot project name for context.")] string? projectName = null,
        CancellationToken cancellationToken = default)
    {
        var combined = string.IsNullOrWhiteSpace(physicsContext)
            ? prompt
            : $"Physics context: {physicsContext.Trim()}{Environment.NewLine}{Environment.NewLine}{prompt}";
        return InvokeAsync(x => x.GenerateGodotPhysicsAsync(new WizardToolRequest(combined, projectName), cancellationToken));
    }

    /// <summary>Generates animation content through the wizard gateway.</summary>
    /// <param name="prompt">Tool prompt describing animation requirements.</param>
    /// <param name="animRoot">Optional animation root hint.</param>
    /// <param name="projectName">Optional project context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Generated content or formatted tool error text.</returns>
    [KernelFunction("generate_animations")]
    [Description("Generates Godot animation keyframes and state machines.")]
    public Task<string> GenerateAnimationsAsync(
        [Description("Describe the animation or state machine needed.")] string prompt,
        [Description("Optional animation root node path.")] string? animRoot = null,
        [Description("Optional Godot project name for context.")] string? projectName = null,
        CancellationToken cancellationToken = default)
    {
        var combined = string.IsNullOrWhiteSpace(animRoot)
            ? prompt
            : $"Animation context (node path): {animRoot.Trim()}{Environment.NewLine}{Environment.NewLine}{prompt}";
        return InvokeAsync(x => x.GenerateAnimationsAsync(new WizardToolRequest(combined, projectName), cancellationToken));
    }

    /// <summary>Generates project scaffolding/configuration through the wizard gateway.</summary>
    /// <param name="prompt">Tool prompt describing project-level work.</param>
    /// <param name="scopeNotes">Optional scope notes.</param>
    /// <param name="projectName">Optional project context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Generated content or formatted tool error text.</returns>
    [KernelFunction("generate_godot_project")]
    [Description("Scaffolds or modifies Godot project structure and configuration.")]
    public Task<string> GenerateGodotProjectAsync(
        [Description("Describe the project structure or config changes needed.")] string prompt,
        [Description("Optional scope notes.")] string? scopeNotes = null,
        [Description("Optional Godot project name for context.")] string? projectName = null,
        CancellationToken cancellationToken = default)
    {
        var combined = string.IsNullOrWhiteSpace(scopeNotes)
            ? prompt
            : $"Project scope notes: {scopeNotes.Trim()}{Environment.NewLine}{Environment.NewLine}{prompt}";
        return InvokeAsync(x => x.GenerateGodotProjectAsync(new WizardToolRequest(combined, projectName), cancellationToken));
    }

    /// <summary>Generates lighting setup through the wizard gateway.</summary>
    /// <param name="prompt">Tool prompt describing lighting requirements.</param>
    /// <param name="lightType">Optional light-type hint.</param>
    /// <param name="projectName">Optional project context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Generated content or formatted tool error text.</returns>
    [KernelFunction("generate_godot_lighting")]
    [Description("Generates Godot lighting and environment setup.")]
    public Task<string> GenerateGodotLightingAsync(
        [Description("Describe the lighting setup required.")] string prompt,
        [Description("Optional light type hint.")] string? lightType = null,
        [Description("Optional Godot project name for context.")] string? projectName = null,
        CancellationToken cancellationToken = default)
    {
        var combined = string.IsNullOrWhiteSpace(lightType)
            ? prompt
            : $"Light type: {lightType.Trim()}{Environment.NewLine}{Environment.NewLine}{prompt}";
        return InvokeAsync(x => x.GenerateGodotLightingAsync(new WizardToolRequest(combined, projectName), cancellationToken));
    }

    /// <summary>Generates camera setup through the wizard gateway.</summary>
    /// <param name="prompt">Tool prompt describing camera requirements.</param>
    /// <param name="cameraType">Optional camera-type hint.</param>
    /// <param name="projectName">Optional project context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Generated content or formatted tool error text.</returns>
    [KernelFunction("generate_godot_camera")]
    [Description("Generates Godot camera setup and viewport layout.")]
    public Task<string> GenerateGodotCameraAsync(
        [Description("Describe the camera setup or viewport layout.")] string prompt,
        [Description("Optional camera type hint.")] string? cameraType = null,
        [Description("Optional Godot project name for context.")] string? projectName = null,
        CancellationToken cancellationToken = default)
    {
        var combined = string.IsNullOrWhiteSpace(cameraType)
            ? prompt
            : $"Camera type: {cameraType.Trim()}{Environment.NewLine}{Environment.NewLine}{prompt}";
        return InvokeAsync(x => x.GenerateGodotCameraAsync(new WizardToolRequest(combined, projectName), cancellationToken));
    }

    /// <summary>Generates shader content through the wizard gateway.</summary>
    /// <param name="prompt">Tool prompt describing shader requirements.</param>
    /// <param name="shaderType">Optional shader-type hint.</param>
    /// <param name="projectName">Optional project context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Generated content or formatted tool error text.</returns>
    [KernelFunction("generate_godot_shaders")]
    [Description("Generates Godot shader code and material setup.")]
    public Task<string> GenerateGodotShadersAsync(
        [Description("Describe the visual shader effect.")] string prompt,
        [Description("Optional shader type hint.")] string? shaderType = null,
        [Description("Optional Godot project name for context.")] string? projectName = null,
        CancellationToken cancellationToken = default)
    {
        var combined = string.IsNullOrWhiteSpace(shaderType)
            ? prompt
            : $"Shader type: {shaderType.Trim()}{Environment.NewLine}{Environment.NewLine}{prompt}";
        return InvokeAsync(x => x.GenerateGodotShadersAsync(new WizardToolRequest(combined, projectName), cancellationToken));
    }

    /// <summary>Generates signal wiring content through the wizard gateway.</summary>
    /// <param name="prompt">Tool prompt describing signal requirements.</param>
    /// <param name="emitterPath">Optional emitter path hint.</param>
    /// <param name="signalNames">Optional signal names hint.</param>
    /// <param name="projectName">Optional project context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Generated content or formatted tool error text.</returns>
    [KernelFunction("generate_godot_signals")]
    [Description("Generates Godot signal declarations, wiring, and handlers.")]
    public Task<string> GenerateGodotSignalsAsync(
        [Description("Describe the signal flow between nodes.")] string prompt,
        [Description("Optional emitter node path.")] string? emitterPath = null,
        [Description("Optional signal names.")] string? signalNames = null,
        [Description("Optional Godot project name for context.")] string? projectName = null,
        CancellationToken cancellationToken = default)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(emitterPath)) parts.Add($"Emitter node: {emitterPath.Trim()}");
        if (!string.IsNullOrWhiteSpace(signalNames)) parts.Add($"Signals: {signalNames.Trim()}");
        parts.Add(prompt);
        return InvokeAsync(x => x.GenerateGodotSignalsAsync(new WizardToolRequest(string.Join(Environment.NewLine + Environment.NewLine, parts), projectName), cancellationToken));
    }

    /// <summary>Generates scene-tree node operations through the wizard gateway.</summary>
    /// <param name="prompt">Tool prompt describing node operations.</param>
    /// <param name="nodeType">Optional node-type hint.</param>
    /// <param name="parentPath">Optional parent-path hint.</param>
    /// <param name="projectName">Optional project context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Generated content or formatted tool error text.</returns>
    [KernelFunction("generate_godot_nodes")]
    [Description("Generates Godot scene-tree node operations and setup.")]
    public Task<string> GenerateGodotNodesAsync(
        [Description("Describe the node operation needed.")] string prompt,
        [Description("Target node type.")] string? nodeType = null,
        [Description("Optional parent path.")] string? parentPath = null,
        [Description("Optional Godot project name for context.")] string? projectName = null,
        CancellationToken cancellationToken = default)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(nodeType)) parts.Add($"Node type: {nodeType.Trim()}");
        if (!string.IsNullOrWhiteSpace(parentPath)) parts.Add($"Parent path: {parentPath.Trim()}");
        parts.Add(prompt);
        return InvokeAsync(x => x.GenerateGodotNodesAsync(new WizardToolRequest(string.Join(Environment.NewLine + Environment.NewLine, parts), projectName), cancellationToken));
    }

    /// <summary>Returns a configuration summary through the wizard gateway.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Human-readable configuration summary or formatted error text.</returns>
    [KernelFunction("get_configuration")]
    [Description("Returns a concise summary of current app configuration.")]
    public Task<string> GetConfigurationAsync(CancellationToken cancellationToken = default) =>
        InvokeAsync(x => x.GetConfigurationAsync(cancellationToken));

    /// <summary>Sets a preference through the wizard gateway.</summary>
    /// <param name="key">Preference key.</param>
    /// <param name="value">Preference value.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Human-readable success/error text.</returns>
    [KernelFunction("set_preference")]
    [Description("Sets an application preference key-value pair.")]
    public Task<string> SetPreferenceAsync(
        [Description("Preference key, e.g. preferred_llm_provider.")] string key,
        [Description("Preference value. Null/empty clears the preference.")] string? value,
        CancellationToken cancellationToken = default) =>
        InvokeAsync(x => x.SetPreferenceAsync(key, value, cancellationToken));

    /// <summary>
    /// Resolves the scoped wizard gateway and executes a plugin tool delegate safely.
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
