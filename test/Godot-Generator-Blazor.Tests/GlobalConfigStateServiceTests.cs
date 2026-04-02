#nullable enable
using Godot_Generator_Blazor.Services;
using Godot_Generator_Blazor.State;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Godot_Generator_Blazor.Tests;

/// <summary>
/// Unit tests for <see cref="GlobalConfigStateService"/>.
/// </summary>
public sealed class GlobalConfigStateServiceTests
{
    /// <summary>
    /// Verifies load operation fetches preferred language from API client.
    /// </summary>
    [Fact]
    public async Task LoadAsync_sets_preferred_language()
    {
        var api = new Mock<IApiClient>();
        api.Setup(x => x.GetGlobalPreferredLanguageAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("gdscript");

        var sut = new GlobalConfigStateService(api.Object, NullLogger<GlobalConfigStateService>.Instance);
        await sut.LoadAsync();

        Assert.Equal("gdscript", sut.PreferredLanguage);
        Assert.Null(sut.Error);
    }

    /// <summary>
    /// Verifies save operation persists the selected language.
    /// </summary>
    [Fact]
    public async Task SaveAsync_persists_selected_language()
    {
        var api = new Mock<IApiClient>();
        api.Setup(x => x.SaveGlobalPreferredLanguageAsync("csharp", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var sut = new GlobalConfigStateService(api.Object, NullLogger<GlobalConfigStateService>.Instance);
        sut.SetPreferredLanguage("csharp");
        var ok = await sut.SaveAsync();

        Assert.True(ok);
        Assert.Null(sut.Error);
    }

    /// <summary>
    /// Verifies save failure sets an error message.
    /// </summary>
    [Fact]
    public async Task SaveAsync_failure_sets_error()
    {
        var api = new Mock<IApiClient>();
        api.Setup(x => x.SaveGlobalPreferredLanguageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var sut = new GlobalConfigStateService(api.Object, NullLogger<GlobalConfigStateService>.Instance);
        var ok = await sut.SaveAsync();

        Assert.False(ok);
        Assert.NotNull(sut.Error);
    }

    /// <summary>
    /// Verifies load failures are captured as state errors.
    /// </summary>
    [Fact]
    public async Task LoadAsync_exception_sets_error()
    {
        var api = new Mock<IApiClient>();
        api.Setup(x => x.GetGlobalPreferredLanguageAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("load fail"));

        var sut = new GlobalConfigStateService(api.Object, NullLogger<GlobalConfigStateService>.Instance);
        await sut.LoadAsync();

        Assert.NotNull(sut.Error);
    }

    /// <summary>
    /// Verifies null assignments normalize to empty language values.
    /// </summary>
    [Fact]
    public void SetPreferredLanguage_null_normalizes_to_empty()
    {
        var sut = new GlobalConfigStateService(new Mock<IApiClient>().Object, NullLogger<GlobalConfigStateService>.Instance);
        sut.SetPreferredLanguage(null!);
        Assert.Equal(string.Empty, sut.PreferredLanguage);
    }

    /// <summary>
    /// Verifies save exceptions are captured and converted to failures.
    /// </summary>
    [Fact]
    public async Task SaveAsync_exception_sets_error()
    {
        var api = new Mock<IApiClient>();
        api.Setup(x => x.SaveGlobalPreferredLanguageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("save fail"));
        var sut = new GlobalConfigStateService(api.Object, NullLogger<GlobalConfigStateService>.Instance);
        var ok = await sut.SaveAsync();
        Assert.False(ok);
        Assert.NotNull(sut.Error);
    }
}
