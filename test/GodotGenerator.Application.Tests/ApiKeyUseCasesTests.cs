#nullable enable
using GodotGenerator.Application;
using GodotGenerator.Application.Abstractions;
using GodotGenerator.Application.Orchestration;
using GodotGenerator.Application.UseCases;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GodotGenerator.Application.Tests;

/// <summary>
/// Unit tests for API-key and configuration application use cases.
/// </summary>
public sealed class ApiKeyUseCasesTests
{
    /// <summary>
    /// Verifies saved API keys are readable through the read use case.
    /// </summary>
    [Fact]
    public async Task Save_then_get_api_keys_roundtrip()
    {
        var repo = new InMemoryPreferenceRepository();
        var save = new SaveApiKeysUseCase(repo, NullLogger<SaveApiKeysUseCase>.Instance);
        var get = new GetApiKeysUseCase(repo, NullLogger<GetApiKeysUseCase>.Instance);

        await save.ExecuteAsync(new Dictionary<string, string?> { ["openai"] = "secret" });
        var keys = await get.ExecuteAsync();

        Assert.Equal("secret", keys["openai"]);
    }

    /// <summary>
    /// Verifies config snapshot includes both preference values and key names.
    /// </summary>
    [Fact]
    public async Task Get_all_config_returns_preferences_and_key_names()
    {
        var repo = new InMemoryPreferenceRepository();
        await repo.SetAsync("preferred_llm_provider", "openai");

        var getPref = new GetPreferenceUseCase(repo, NullLogger<GetPreferenceUseCase>.Instance);
        var getKeys = new GetApiKeysUseCase(repo, NullLogger<GetApiKeysUseCase>.Instance);
        var saveKeys = new SaveApiKeysUseCase(repo, NullLogger<SaveApiKeysUseCase>.Instance);
        await saveKeys.ExecuteAsync(new Dictionary<string, string?> { ["openai"] = "secret" });

        var useCase = new GetAllConfigUseCase(getKeys, getPref, new DefaultLlmDiscoveryInfoProvider());
        var snapshot = await useCase.ExecuteAsync();

        Assert.Equal("openai", snapshot.Preferences["preferred_llm_provider"]);
        Assert.Contains("openai", snapshot.KeyNames);
        Assert.Equal("openai", snapshot.DefaultLlmProvider);
        Assert.Equal("gpt-4o-mini", snapshot.DefaultChatModelId);
    }

    /// <summary>
    /// When <c>preferred_language</c> is unset, legacy <c>preferred_locale</c> is merged into the snapshot.
    /// </summary>
    [Fact]
    public async Task Get_all_config_merges_legacy_preferred_locale_into_preferred_language()
    {
        var repo = new InMemoryPreferenceRepository();
        await repo.SetAsync(PreferenceKeys.PreferredLocaleLegacy, "gdscript");

        var getPref = new GetPreferenceUseCase(repo, NullLogger<GetPreferenceUseCase>.Instance);
        var getKeys = new GetApiKeysUseCase(repo, NullLogger<GetApiKeysUseCase>.Instance);
        var useCase = new GetAllConfigUseCase(getKeys, getPref, new DefaultLlmDiscoveryInfoProvider());
        var snapshot = await useCase.ExecuteAsync();

        Assert.Equal("gdscript", snapshot.Preferences[PreferenceKeys.PreferredLanguage]);
    }

    /// <summary>
    /// <c>preferred_language</c> wins when both legacy and canonical keys exist.
    /// </summary>
    [Fact]
    public async Task Get_all_config_prefers_preferred_language_over_legacy_locale()
    {
        var repo = new InMemoryPreferenceRepository();
        await repo.SetAsync(PreferenceKeys.PreferredLocaleLegacy, "gdscript");
        await repo.SetAsync(PreferenceKeys.PreferredLanguage, "csharp");

        var getPref = new GetPreferenceUseCase(repo, NullLogger<GetPreferenceUseCase>.Instance);
        var getKeys = new GetApiKeysUseCase(repo, NullLogger<GetApiKeysUseCase>.Instance);
        var useCase = new GetAllConfigUseCase(getKeys, getPref, new DefaultLlmDiscoveryInfoProvider());
        var snapshot = await useCase.ExecuteAsync();

        Assert.Equal("csharp", snapshot.Preferences[PreferenceKeys.PreferredLanguage]);
    }

    /// <summary>
    /// Minimal in-memory implementation for preference repository testing.
    /// </summary>
    private sealed class InMemoryPreferenceRepository : IPreferenceRepository
    {
        private readonly Dictionary<string, string?> _values = new(StringComparer.Ordinal);

        /// <inheritdoc />
        public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
        {
            _values.TryGetValue(key, out var value);
            return Task.FromResult(value);
        }

        /// <inheritdoc />
        public Task SetAsync(string key, string? value, CancellationToken cancellationToken = default)
        {
            if (value is null)
            {
                _values.Remove(key);
            }
            else
            {
                _values[key] = value;
            }

            return Task.CompletedTask;
        }
    }
}
