#nullable enable
using System.ComponentModel.DataAnnotations;

namespace GodotGenerator.Infrastructure.Ai.Options;

/// <summary>
/// Configuration for orchestration runtime behavior.
/// </summary>
public sealed class OrchestrationOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Orchestration";

    /// <summary>Used when configuration binds an empty <see cref="GenericFailureMessage"/>.</summary>
    public const string DefaultGenericFailureMessage = "Agent turn failed. Check logs for details.";

    /// <summary>
    /// Enables Semantic Kernel automatic tool invocation.
    /// </summary>
    public bool EnableAutoToolInvocation { get; set; } = true;

    /// <summary>
    /// Generic user-safe message returned when orchestration fails.
    /// </summary>
    [Required]
    public string GenericFailureMessage { get; set; } = DefaultGenericFailureMessage;

    /// <summary>
    /// Maximum duration for one orchestration turn. Zero or negative disables timeout.
    /// </summary>
    public int TurnTimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// When true, registered Godot MCP kernel functions are narrowed by modality policy for known <c>godot-*</c> modalities.
    /// </summary>
    public bool EnableModalityToolFiltering { get; set; } = true;
}
