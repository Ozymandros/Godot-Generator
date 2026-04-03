#nullable enable

namespace GodotGenerator.Infrastructure.Ai.Services;

/// <summary>
/// Evaluates provider/modality support in the in-process runtime.
/// </summary>
public interface IProviderCapabilityRouter
{
    /// <summary>
    /// Returns whether the provider supports the requested modality.
    /// </summary>
    bool Supports(string? provider, string? modality, out string? reason);
}

