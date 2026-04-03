#nullable enable

namespace Godot_Generator_Avalonia.Models;

/// <summary>
/// Aggregated settings from <see cref="GodotGenerator.Api.Abstractions.IGodotGeneratorApiService.GetAllConfigAsync"/> for the desktop UI.
/// </summary>
public sealed record SettingsSnapshot(
    IReadOnlyDictionary<string, string?> Preferences,
    IReadOnlyList<string> KeyNames,
    string DefaultLlmProvider,
    string DefaultChatModelId,
    IReadOnlyList<string> GodotToolNames);
