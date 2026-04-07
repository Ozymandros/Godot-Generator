using GodotGenerator.Api.Abstractions;
using GodotGenerator.Api.Dtos;
using GodotGenerator.Blazor.Infrastructure.DesktopIpc.Handlers;
using GodotGenerator.Desktop.Contracts.Commands;
using GodotGenerator.Desktop.Contracts.Envelope;
using GodotGenerator.Desktop.Contracts.Serialization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace GodotGenerator.Desktop.Ipc.Tests.Handlers;

public sealed class KeysCommandHandlerTests
{
    private static KeysCommandHandler Build(IGodotGeneratorApiService api) =>
        new(api, NullLogger<KeysCommandHandler>.Instance);

    private static string Json<T>(T obj) =>
        System.Text.Json.JsonSerializer.Serialize(obj, ContractJsonOptions.Default);

    [Fact]
    public void CommandNames_ContainsSave()
    {
        var h = Build(new Mock<IGodotGeneratorApiService>().Object);
        Assert.Contains(KeysCommandNames.Save, h.CommandNames);
    }

    [Fact]
    public async Task HandleAsync_ValidRequest_ReturnsSuccess()
    {
        var api = new Mock<IGodotGeneratorApiService>();
        api.Setup(a => a.SaveApiKeysAsync(It.IsAny<ApiKeysRequest>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(ApiResponse<Dictionary<string, IReadOnlyList<string>>>.Ok(
               new Dictionary<string, IReadOnlyList<string>> { ["saved"] = ["openai"] }));

        var h   = Build(api.Object);
        var env = new CommandEnvelope("id1", KeysCommandNames.Save,
                      Json(new KeysSaveRequest(new Dictionary<string, string?> { ["openai"] = "sk-test" })));
        var result = await h.HandleAsync(env, CancellationToken.None);

        Assert.True(result.Success);
        api.Verify(a => a.SaveApiKeysAsync(
            It.Is<ApiKeysRequest>(r => r.Keys.ContainsKey("openai")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_EmptyKeys_ReturnsValidationError()
    {
        var h   = Build(new Mock<IGodotGeneratorApiService>().Object);
        var env = new CommandEnvelope("id2", KeysCommandNames.Save,
                      Json(new KeysSaveRequest(new Dictionary<string, string?>())));
        var result = await h.HandleAsync(env, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(DesktopErrorCode.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task HandleAsync_NullPayload_ReturnsValidationError()
    {
        var h   = Build(new Mock<IGodotGeneratorApiService>().Object);
        var env = new CommandEnvelope("id3", KeysCommandNames.Save, null);
        var result = await h.HandleAsync(env, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(DesktopErrorCode.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task HandleAsync_ApiFailure_ReturnsHandlerFaulted()
    {
        var api = new Mock<IGodotGeneratorApiService>();
        api.Setup(a => a.SaveApiKeysAsync(It.IsAny<ApiKeysRequest>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(ApiResponse<Dictionary<string, IReadOnlyList<string>>>.Fail("store error"));

        var h   = Build(api.Object);
        var env = new CommandEnvelope("id4", KeysCommandNames.Save,
                      Json(new KeysSaveRequest(new Dictionary<string, string?> { ["x"] = "val" })));
        var result = await h.HandleAsync(env, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(DesktopErrorCode.HandlerFaulted, result.ErrorCode);
    }
}
