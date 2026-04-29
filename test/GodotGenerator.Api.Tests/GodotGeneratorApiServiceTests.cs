#nullable enable
using GodotGenerator.Api.Dtos;
using GodotGenerator.Api.Services;
using GodotGenerator.Application.Abstractions;
using GodotGenerator.Application.Dtos;
using GodotGenerator.Application.Orchestration;
using GodotGenerator.Application.UseCases;
using GodotGenerator.Application;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
// WizardResult, WizardRequest are in GodotGenerator.Application.Dtos (already imported above)

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
    /// Verifies provider and model preferences propagate into orchestration input.
    /// </summary>
    [Fact]
    public async Task GenerateImageAsync_propagates_effective_provider_and_model_to_turn_request()
    {
        var repo = new InMemoryPreferenceRepository();
        await repo.SetAsync("preferred_image_provider", "stability");
        await repo.SetAsync("preferred_image_model", "sdxl");

        AgentTurnRequest? captured = null;
        var ai = new Mock<IAiOrchestrationService>();
        ai.Setup(x => x.RunTurnAsync(It.IsAny<AgentTurnRequest>(), It.IsAny<CancellationToken>()))
            .Callback<AgentTurnRequest, CancellationToken>((req, _) => captured = req)
            .ReturnsAsync(new AgentTurnResult(true, "ok"));

        var api = CreateApiService(repo, ai.Object);
        var result = await api.GenerateImageAsync(new GenerateRequest("paint"));

        Assert.True(result.Success);
        Assert.NotNull(captured);
        Assert.Equal("stability", captured!.Provider);
        Assert.Equal("sdxl", captured.PreferredModelId);
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

    /// <summary>
    /// EnhancePromptAsync returns success with result and mode fields.
    /// </summary>
    [Fact]
    public async Task EnhancePromptAsync_returns_ok_with_result_and_mode()
    {
        var promptAssist = new Mock<IPromptAssistService>();
        promptAssist
            .Setup(s => s.EnhanceAsync(It.IsAny<PromptAssistRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PromptAssistResult.Ok("improved prompt text", "improve"));

        var api = CreateApiService(
            new InMemoryPreferenceRepository(),
            new Mock<IAiOrchestrationService>().Object,
            promptAssist.Object);

        var result = await api.EnhancePromptAsync(new PromptAssistRequest("code", "do stuff"));

        Assert.True(result.Success);
        Assert.Equal("improved prompt text", result.Data!["result"]?.ToString());
        Assert.Equal("improve",              result.Data!["mode"]?.ToString());
    }

    /// <summary>
    /// EnhancePromptAsync propagates service failures as API failures.
    /// </summary>
    [Fact]
    public async Task EnhancePromptAsync_service_failure_returns_api_failure()
    {
        var promptAssist = new Mock<IPromptAssistService>();
        promptAssist
            .Setup(s => s.EnhanceAsync(It.IsAny<PromptAssistRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PromptAssistResult.Fail("no key configured"));

        var api = CreateApiService(
            new InMemoryPreferenceRepository(),
            new Mock<IAiOrchestrationService>().Object,
            promptAssist.Object);

        var result = await api.EnhancePromptAsync(new PromptAssistRequest("code", "anything"));

        Assert.False(result.Success);
        Assert.Contains("no key configured", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// All five new Godot modalities route through GenerateByModality correctly (smoke test).
    /// </summary>
    [Theory]
    [InlineData("godot-lighting")]
    [InlineData("godot-camera")]
    [InlineData("godot-shaders")]
    [InlineData("godot-signals")]
    [InlineData("godot-nodes")]
    public async Task Generate_new_godot_modalities_succeed_and_return_modality_key(string modalityKey)
    {
        var ai = new Mock<IAiOrchestrationService>();
        ai.Setup(x => x.RunTurnAsync(It.IsAny<AgentTurnRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentTurnResult(true, "ok"));

        var api = CreateApiService(new InMemoryPreferenceRepository(), ai.Object);

        // Route to the correct method based on modalityKey
        var result = modalityKey switch
        {
            "godot-lighting" => await api.GenerateGodotLightingAsync(new GenerateRequest("add lights")),
            "godot-camera"   => await api.GenerateGodotCameraAsync(new GenerateRequest("setup cam")),
            "godot-shaders"  => await api.GenerateGodotShadersAsync(new GenerateRequest("write shader")),
            "godot-signals"  => await api.GenerateGodotSignalsAsync(new GenerateRequest("connect signal")),
            "godot-nodes"    => await api.GenerateGodotNodesAsync(new GenerateRequest("add node")),
            _                => throw new InvalidOperationException($"Unknown modality: {modalityKey}"),
        };

        Assert.True(result.Success);
        Assert.Equal(modalityKey, result.Data!["modality"]?.ToString());
    }

    /// <summary>RunWizardAsync returns success with message and toolsInvoked fields.</summary>
    [Fact]
    public async Task RunWizardAsync_returns_ok_with_message_and_tools_invoked()
    {
        var wizard = new Mock<IWizardOrchestrationService>();
        wizard
            .Setup(s => s.RunAsync(It.IsAny<WizardRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WizardResult.Ok("Scene + code generated.", ["GodotGeneratorApi.create_scene"]));

        var api = CreateApiService(
            new InMemoryPreferenceRepository(),
            new Mock<IAiOrchestrationService>().Object,
            wizardSvc: wizard.Object);

        var result = await api.RunWizardAsync(new WizardRequest("Build a platformer"));

        Assert.True(result.Success);
        Assert.Contains("Scene", result.Data!["message"]?.ToString());
        Assert.Equal("wizard", result.Data!["modality"]?.ToString());
    }

    /// <summary>RunWizardAsync propagates service failures as API failures.</summary>
    [Fact]
    public async Task RunWizardAsync_service_failure_returns_api_failure()
    {
        var wizard = new Mock<IWizardOrchestrationService>();
        wizard
            .Setup(s => s.RunAsync(It.IsAny<WizardRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WizardResult.Fail("API key missing"));

        var api = CreateApiService(
            new InMemoryPreferenceRepository(),
            new Mock<IAiOrchestrationService>().Object,
            wizardSvc: wizard.Object);

        var result = await api.RunWizardAsync(new WizardRequest("do something"));

        Assert.False(result.Success);
    }

    /// <summary>
    /// Legacy wizard endpoint contract remains stable and applies provider/model preferences.
    /// </summary>
    [Fact]
    public async Task RunWizardAsync_resolves_provider_and_model_from_preferences()
    {
        var repo = new InMemoryPreferenceRepository();
        await repo.SetAsync("preferred_llm_provider", "openai");
        await repo.SetAsync("preferred_llm_model", "gpt-4.1");

        WizardRequest? captured = null;
        var wizard = new Mock<IWizardOrchestrationService>();
        wizard
            .Setup(s => s.RunAsync(It.IsAny<WizardRequest>(), It.IsAny<CancellationToken>()))
            .Callback<WizardRequest, CancellationToken>((req, _) => captured = req)
            .ReturnsAsync(WizardResult.Ok("ok", []));

        var api = CreateApiService(
            repo,
            new Mock<IAiOrchestrationService>().Object,
            wizardSvc: wizard.Object);

        var result = await api.RunWizardAsync(new WizardRequest("Build menu flow"));

        Assert.True(result.Success);
        Assert.NotNull(captured);
        Assert.Equal("openai", captured!.Provider);
        Assert.Equal("gpt-4.1", captured.PreferredModelId);
        Assert.Equal("wizard", result.Data!["modality"]?.ToString());
        Assert.NotNull(result.Data!["toolsInvoked"]);
    }

    private static GodotGeneratorApiService CreateApiService(
        IPreferenceRepository preferences,
        IAiOrchestrationService ai,
        IPromptAssistService? promptAssist = null,
        IWizardOrchestrationService? wizardSvc = null)
    {
        var run = new RunAgentTurnUseCase(ai, NullLogger<RunAgentTurnUseCase>.Instance);
        var getPref = new GetPreferenceUseCase(preferences, NullLogger<GetPreferenceUseCase>.Instance);
        var setPref = new SetPreferenceUseCase(preferences, NullLogger<SetPreferenceUseCase>.Instance);
        var getKeys = new GetApiKeysUseCase(preferences, NullLogger<GetApiKeysUseCase>.Instance);
        var saveKeys = new SaveApiKeysUseCase(preferences, NullLogger<SaveApiKeysUseCase>.Instance);
        var llmDiscovery = new DefaultLlmDiscoveryInfoProvider();
        var all = new GetAllConfigUseCase(getKeys, getPref, llmDiscovery);
        var assist = promptAssist ?? new Mock<IPromptAssistService>().Object;
        var enhance = new EnhancePromptUseCase(assist, NullLogger<EnhancePromptUseCase>.Instance);
        var wizardOrch = wizardSvc ?? new Mock<IWizardOrchestrationService>().Object;
        var runWizard = new RunWizardUseCase(wizardOrch, NullLogger<RunWizardUseCase>.Instance);
        var composer = new ModalityTurnComposer();
        var catalog = new NullGodotMcpToolCatalog();

        return new GodotGeneratorApiService(
            run,
            getPref,
            setPref,
            getKeys,
            saveKeys,
            all,
            enhance,
            runWizard,
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
