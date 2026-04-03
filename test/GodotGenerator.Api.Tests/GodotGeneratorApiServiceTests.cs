#nullable enable
using GodotGenerator.Api.Dtos;
using GodotGenerator.Api.Services;
using GodotGenerator.Application.Abstractions;
using GodotGenerator.Application.Dtos;
using GodotGenerator.Application.Orchestration;
using GodotGenerator.Application.UseCases;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GodotGenerator.Api.Tests;

/// <summary>
/// Unit tests for <see cref="GodotGeneratorApiService"/>.
/// </summary>
public sealed class GodotGeneratorApiServiceTests
{
    /// <summary>
    /// Verifies empty prompts are rejected with a failed response.
    /// </summary>
    [Fact]
    public async Task GenerateTextAsync_empty_prompt_returns_failure()
    {
        var api = CreateApiService(new InMemoryPreferenceRepository(), new Mock<IAiOrchestrationService>().Object);
        var result = await api.GenerateTextAsync(new GenerateRequest("   "));
        Assert.False(result.Success);
    }

    /// <summary>
    /// Verifies provider fallback from preferences and successful generation envelope.
    /// </summary>
    [Fact]
    public async Task GenerateTextAsync_uses_preferred_provider_from_preferences()
    {
        var repo = new InMemoryPreferenceRepository();
        await repo.SetAsync("preferred_llm_provider", "openai");

        var ai = new Mock<IAiOrchestrationService>();
        ai.Setup(x => x.RunTurnAsync(It.IsAny<AgentTurnRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentTurnResult(true, "ok"));

        var api = CreateApiService(repo, ai.Object);
        var result = await api.GenerateTextAsync(new GenerateRequest("hello"));

        Assert.True(result.Success);
        Assert.Equal("openai", result.Data!["provider"] as string);
        Assert.Equal("ok", result.Data!["message"] as string);
    }

    /// <summary>
    /// Verifies key save and retrieval are consistent.
    /// </summary>
    [Fact]
    public async Task SaveApiKeysAsync_then_GetApiKeysAsync_roundtrip()
    {
        var api = CreateApiService(new InMemoryPreferenceRepository(), new Mock<IAiOrchestrationService>().Object);
        var save = await api.SaveApiKeysAsync(new ApiKeysRequest(new Dictionary<string, string?> { ["openai"] = "k1" }));
        var get = await api.GetApiKeysAsync();

        Assert.True(save.Success);
        Assert.True(get.Success);
        Assert.Equal("k1", get.Data!["openai"]);
    }

    private static GodotGeneratorApiService CreateApiService(
        IPreferenceRepository preferences,
        IAiOrchestrationService ai)
    {
        var run = new RunAgentTurnUseCase(ai, NullLogger<RunAgentTurnUseCase>.Instance);
        var getPref = new GetPreferenceUseCase(preferences, NullLogger<GetPreferenceUseCase>.Instance);
        var setPref = new SetPreferenceUseCase(preferences, NullLogger<SetPreferenceUseCase>.Instance);
        var getKeys = new GetApiKeysUseCase(preferences, NullLogger<GetApiKeysUseCase>.Instance);
        var saveKeys = new SaveApiKeysUseCase(preferences, NullLogger<SaveApiKeysUseCase>.Instance);
        var llmDiscovery = new DefaultLlmDiscoveryInfoProvider();
        var all = new GetAllConfigUseCase(getKeys, getPref, llmDiscovery);
        var composer = new ModalityTurnComposer();
        var catalog = new NullGodotMcpToolCatalog();

        return new GodotGeneratorApiService(
            run,
            getPref,
            setPref,
            getKeys,
            saveKeys,
            all,
            composer,
            catalog,
            NullLogger<GodotGeneratorApiService>.Instance);
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
