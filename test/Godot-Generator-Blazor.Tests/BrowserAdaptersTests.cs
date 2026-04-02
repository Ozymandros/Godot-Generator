#nullable enable
using Godot_Generator_Blazor.Services;
using Xunit;

namespace Godot_Generator_Blazor.Tests;

/// <summary>
/// Unit tests for browser adapter implementations used in direct-WASM mode.
/// </summary>
public sealed class BrowserAdaptersTests
{
    /// <summary>
    /// Verifies in-memory preference repository set/get/remove behavior.
    /// </summary>
    [Fact]
    public async Task BrowserPreferenceRepository_roundtrip_and_remove()
    {
        var repo = new BrowserPreferenceRepository();
        await repo.SetAsync("k", "v");
        Assert.Equal("v", await repo.GetAsync("k"));

        await repo.SetAsync("k", null);
        Assert.Null(await repo.GetAsync("k"));
    }

    /// <summary>
    /// Verifies browser orchestration rejects empty prompts.
    /// </summary>
    [Fact]
    public async Task BrowserAiOrchestrationService_empty_prompt_returns_failure()
    {
        var sut = new BrowserAiOrchestrationService();
        var result = await sut.RunTurnAsync(new GodotGenerator.Application.Dtos.AgentTurnRequest(" "));
        Assert.False(result.Success);
    }

    /// <summary>
    /// Verifies browser orchestration returns deterministic success output for non-empty prompts.
    /// </summary>
    [Fact]
    public async Task BrowserAiOrchestrationService_non_empty_prompt_returns_success()
    {
        var sut = new BrowserAiOrchestrationService();
        var result = await sut.RunTurnAsync(new GodotGenerator.Application.Dtos.AgentTurnRequest("hello"));
        Assert.True(result.Success);
        Assert.Contains("hello", result.Message, StringComparison.Ordinal);
    }
}
