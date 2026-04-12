#nullable enable

using GodotGenerator.Application;
using GodotGenerator.Application.Abstractions;
using GodotGenerator.Application.Configuration;
using GodotGenerator.Application.Services;

namespace GodotGenerator.Infrastructure.Ai.Services;

/// <summary>
/// Resolves in-process runtime support from the persisted provider registry:
/// <see cref="ProviderRegistryEntry.OpenAiCompatibility"/>.
/// </summary>
public sealed class ProviderCapabilityRouter(IPreferenceRepository preferences) : IProviderCapabilityRouter
{
    private const string DefaultProviderId = "openai";

    /// <inheritdoc />
    public bool Supports(string? provider, string? modality, out string? reason)
    {
        _ = modality;
        reason = null;
        var effectiveProvider = string.IsNullOrWhiteSpace(provider) ? DefaultProviderId : provider.Trim();

        var json = preferences
            .GetAsync(PreferenceKeys.ProvidersRegistryV1, CancellationToken.None)
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();

        var doc = ConfigurationRegistryService.ParseProviderRegistry(json);
        var entry = FindProvider(doc.Providers, effectiveProvider);
        if (entry is null)
        {
            reason =
                $"Provider '{effectiveProvider}' is not listed in the provider registry (Settings → Providers).";
            return false;
        }

        if (!entry.OpenAiCompatibility)
        {
            reason =
                $"Provider '{effectiveProvider}' is not marked OpenAI-compatible; the in-process Semantic Kernel runtime requires openAiCompatibility.";
            return false;
        }

        return true;
    }

    private static ProviderRegistryEntry? FindProvider(IReadOnlyList<ProviderRegistryEntry> providers, string id)
    {
        foreach (var p in providers)
        {
            if (string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase))
            {
                return p;
            }
        }

        return null;
    }
}
