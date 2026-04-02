#nullable enable
using System.ComponentModel.DataAnnotations;

namespace GodotGenerator.Infrastructure.Ai.Options;

/// <summary>
/// Configuration for OpenAI-compatible chat completion used with Semantic Kernel.
/// </summary>
public sealed class LlmOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Llm";

    /// <summary>
    /// Chat model id (e.g. gpt-4o-mini).
    /// </summary>
    [Required]
    [MinLength(1)]
    public string ChatModelId { get; set; } = "gpt-4o-mini";

    /// <summary>
    /// API key for the OpenAI-compatible endpoint.
    /// </summary>
    [Required]
    [MinLength(1)]
    public string ApiKey { get; set; } = "";

    /// <summary>
    /// Optional org id for OpenAI.
    /// </summary>
    public string? OrganizationId { get; set; }
}
