#nullable enable

namespace GodotGenerator.Application.Configuration;

/// <summary>Aggregated backend configuration snapshot for discovery and settings UI.</summary>
public sealed record AllConfigSnapshot(
    IReadOnlyDictionary<string, string?> Preferences,
    IReadOnlyList<string> KeyNames,
    string DefaultLlmProvider,
    string DefaultChatModelId,
    IReadOnlyList<ProviderRegistryEntry> ProviderRegistry,
    IReadOnlyDictionary<string, IReadOnlyList<ModelRegistryEntry>> ModelsByProvider,
    IReadOnlyDictionary<string, string?> SystemPrompts);
