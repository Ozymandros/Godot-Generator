#nullable enable

namespace GodotGenerator.Infrastructure.Ai.Services;

/// <summary>
/// Phase-1 provider capability router for in-process runtimes.
/// </summary>
public sealed class ProviderCapabilityRouter : IProviderCapabilityRouter
{
    private static readonly HashSet<string> OpenAiCompatibleProviders = new(StringComparer.OrdinalIgnoreCase)
    {
        "openai",
    };

    /// <inheritdoc />
    public bool Supports(string? provider, string? modality, out string? reason)
    {
        reason = null;
        var effectiveProvider = string.IsNullOrWhiteSpace(provider) ? "openai" : provider.Trim();
        if (OpenAiCompatibleProviders.Contains(effectiveProvider))
        {
            return true;
        }

        reason = $"Provider '{effectiveProvider}' is not yet supported in the current in-process runtime.";
        return false;
    }
}

