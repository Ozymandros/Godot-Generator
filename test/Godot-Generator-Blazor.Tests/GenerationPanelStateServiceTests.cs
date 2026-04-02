#nullable enable
using Godot_Generator_Blazor.Models;
using Godot_Generator_Blazor.Services;
using Godot_Generator_Blazor.State;
using Moq;
using Xunit;

namespace Godot_Generator_Blazor.Tests;

/// <summary>
/// Unit tests for <see cref="GenerationPanelStateService"/>.
/// </summary>
public sealed class GenerationPanelStateServiceTests
{
    /// <summary>
    /// Verifies service initializes one panel per modality.
    /// </summary>
    [Fact]
    public void All_returns_one_panel_per_modality()
    {
        var sut = new GenerationPanelStateService(new Mock<IApiClient>().Object);
        Assert.Equal(Enum.GetValues<GenerationModality>().Length, sut.All.Count);
    }

    /// <summary>
    /// Verifies successful submit writes response text and clears errors.
    /// </summary>
    [Fact]
    public async Task SubmitAsync_success_updates_response()
    {
        var api = new Mock<IApiClient>();
        api.Setup(x => x.GenerateAsync(
                GenerationModality.Text,
                "hello",
                "gdscript",
                "csharp",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, "ok", null));

        var sut = new GenerationPanelStateService(api.Object);
        var panel = sut.Get(GenerationModality.Text);
        panel.Prompt = "hello";
        panel.PreferredLanguageOverride = "gdscript";

        await sut.SubmitAsync(panel, "csharp");

        Assert.Equal("ok", panel.ResponseText);
        Assert.Null(panel.Error);
        Assert.False(panel.IsBusy);
    }

    /// <summary>
    /// Verifies failed submit writes panel error.
    /// </summary>
    [Fact]
    public async Task SubmitAsync_failure_updates_error()
    {
        var api = new Mock<IApiClient>();
        api.Setup(x => x.GenerateAsync(
                GenerationModality.Text,
                "hello",
                string.Empty,
                "csharp",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((false, string.Empty, "boom"));

        var sut = new GenerationPanelStateService(api.Object);
        var panel = sut.Get(GenerationModality.Text);
        panel.Prompt = "hello";

        await sut.SubmitAsync(panel, "csharp");

        Assert.Equal("boom", panel.Error);
        Assert.Null(panel.ResponseText);
        Assert.False(panel.IsBusy);
    }

    /// <summary>
    /// Verifies exceptions from API client bubble to caller while busy flag is reset.
    /// </summary>
    [Fact]
    public async Task SubmitAsync_exception_resets_busy()
    {
        var api = new Mock<IApiClient>();
        api.Setup(x => x.GenerateAsync(
                It.IsAny<GenerationModality>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var sut = new GenerationPanelStateService(api.Object);
        var panel = sut.Get(GenerationModality.Text);
        panel.Prompt = "hello";

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.SubmitAsync(panel, "csharp"));
        Assert.False(panel.IsBusy);
    }

    /// <summary>
    /// Verifies failed submissions can produce null error messages without crashes.
    /// </summary>
    [Fact]
    public async Task SubmitAsync_failure_with_null_error_is_safe()
    {
        var api = new Mock<IApiClient>();
        api.Setup(x => x.GenerateAsync(
                It.IsAny<GenerationModality>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((false, string.Empty, null));

        var sut = new GenerationPanelStateService(api.Object);
        var panel = sut.Get(GenerationModality.Text);
        panel.Prompt = "hello";

        await sut.SubmitAsync(panel, "csharp");
        Assert.Null(panel.Error);
        Assert.Null(panel.ResponseText);
    }
}
