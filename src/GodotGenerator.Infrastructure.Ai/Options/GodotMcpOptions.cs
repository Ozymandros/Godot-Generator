#nullable enable

namespace GodotGenerator.Infrastructure.Ai.Options;

/// <summary>
/// Configuration options for the Godot MCP auto-invocation pipeline.
/// Bind from the <c>GodotMcp</c> configuration section.
/// </summary>
public sealed class GodotMcpOptions
{
    /// <summary>
    /// Configuration section name used when binding from <c>appsettings.json</c>.
    /// </summary>
    public const string SectionName = "GodotMcp";

    /// <summary>
    /// When <see langword="true"/>, the auto-invoker will scan completed LLM responses for
    /// GDScript code blocks and automatically call the appropriate Godot MCP tool to persist
    /// or attach the generated code. Defaults to <see langword="true"/>.
    /// </summary>
    public bool EnableAutoInvokeGeneratedCode { get; set; } = true;

    /// <summary>
    /// When <see langword="true"/>, the auto-invoker logs the function it <em>would</em> call
    /// but does not actually invoke it. Useful for diagnosing candidate-selection without
    /// side-effects. Defaults to <see langword="false"/>.
    /// </summary>
    public bool AutoInvokeDryRun { get; set; } = false;
}
