using GodotGenerator.Api.Abstractions;
using GodotGenerator.Api.Dtos;
using GodotGenerator.Blazor.Infrastructure.DesktopIpc.Handlers;
using GodotGenerator.Desktop.Contracts.Commands;
using GodotGenerator.Desktop.Contracts.Envelope;
using GodotGenerator.Desktop.Contracts.Serialization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace GodotGenerator.Desktop.Ipc.Tests.Handlers;

public sealed class PreferenceCommandHandlerTests
{
    private static PreferenceCommandHandler Build(IGodotGeneratorApiService api) =>
        new(api, NullLogger<PreferenceCommandHandler>.Instance);

    private static string Json<T>(T obj) =>
        System.Text.Json.JsonSerializer.Serialize(obj, ContractJsonOptions.Default);

    // ── CommandNames ──────────────────────────────────────────────────────────

    [Fact]
    public void CommandNames_ContainsGetAndSet()
    {
        var h = Build(new Mock<IGodotGeneratorApiService>().Object);
        Assert.Contains(PreferenceCommandNames.Get, h.CommandNames);
        Assert.Contains(PreferenceCommandNames.Set, h.CommandNames);
    }

    // ── Get ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_Get_ValidKey_ReturnsValue()
    {
        var api = new Mock<IGodotGeneratorApiService>();
        api.Setup(a => a.GetPreferenceAsync("theme", It.IsAny<CancellationToken>()))
           .ReturnsAsync(ApiResponse<Dictionary<string, string?>>.Ok(
               new Dictionary<string, string?> { ["key"] = "theme", ["value"] = "dark" }));

        var h   = Build(api.Object);
        var env = new CommandEnvelope("id1", PreferenceCommandNames.Get,
                      Json(new PreferenceGetRequest("theme")));
        var result = await h.HandleAsync(env, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains("dark", result.PayloadJson ?? "");
    }

    [Fact]
    public async Task HandleAsync_Get_MissingPayload_ReturnsValidationError()
    {
        var h   = Build(new Mock<IGodotGeneratorApiService>().Object);
        var env = new CommandEnvelope("id2", PreferenceCommandNames.Get, null);
        var result = await h.HandleAsync(env, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(DesktopErrorCode.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task HandleAsync_Get_ApiFailure_ReturnsHandlerFaulted()
    {
        var api = new Mock<IGodotGeneratorApiService>();
        api.Setup(a => a.GetPreferenceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(ApiResponse<Dictionary<string, string?>>.Fail("store error"));

        var h   = Build(api.Object);
        var env = new CommandEnvelope("id3", PreferenceCommandNames.Get,
                      Json(new PreferenceGetRequest("key1")));
        var result = await h.HandleAsync(env, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(DesktopErrorCode.HandlerFaulted, result.ErrorCode);
    }

    // ── Set ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_Set_ValidRequest_CallsApiAndSucceeds()
    {
        var api = new Mock<IGodotGeneratorApiService>();
        api.Setup(a => a.SetPreferenceAsync(It.IsAny<SetPreferenceRequest>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(ApiResponse<Dictionary<string, string?>>.Ok(
               new Dictionary<string, string?> { ["key"] = "lang", ["value"] = "en" }));

        var h   = Build(api.Object);
        var env = new CommandEnvelope("id4", PreferenceCommandNames.Set,
                      Json(new PreferenceSetRequest("lang", "en")));
        var result = await h.HandleAsync(env, CancellationToken.None);

        Assert.True(result.Success);
        api.Verify(a => a.SetPreferenceAsync(
            It.Is<SetPreferenceRequest>(r => r.Key == "lang" && r.Value == "en"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Set_MissingPayload_ReturnsValidationError()
    {
        var h   = Build(new Mock<IGodotGeneratorApiService>().Object);
        var env = new CommandEnvelope("id5", PreferenceCommandNames.Set, null);
        var result = await h.HandleAsync(env, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(DesktopErrorCode.ValidationFailed, result.ErrorCode);
    }
}
