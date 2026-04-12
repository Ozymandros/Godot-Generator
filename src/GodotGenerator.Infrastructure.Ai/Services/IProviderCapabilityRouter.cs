#nullable enable

namespace GodotGenerator.Infrastructure.Ai.Services;

/// <summary>
/// Evaluates whether a provider can be used by the in-process runtime (Semantic Kernel),
/// using <see cref="GodotGenerator.Application.Configuration.ProviderRegistryEntry.OpenAiCompatibility"/>.
/// </summary>
public interface IProviderCapabilityRouter
{
    /// <summary>
    /// Returns whether the provider is registered and marked OpenAI-compatible for the in-process stack.
    /// </summary>
    /// <param name="provider">Registry provider id, or null/empty to use the default (openai).</param>
    /// <param name="modality">Reserved for future modality-specific rules; ignored for now.</param>
    /// <param name="reason">Human-readable explanation when this method returns false.</param>
    bool Supports(string? provider, string? modality, out string? reason);
}

