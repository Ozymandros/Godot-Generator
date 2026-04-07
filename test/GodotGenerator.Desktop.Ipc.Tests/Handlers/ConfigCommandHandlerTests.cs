using GodotGenerator.Api.Abstractions;
using GodotGenerator.Api.Dtos;
using GodotGenerator.Blazor.Infrastructure.DesktopIpc.Handlers;
using GodotGenerator.Desktop.Contracts.Commands;
using GodotGenerator.Desktop.Contracts.Envelope;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace GodotGenerator.Desktop.Ipc.Tests.Handlers;

public sealed class ConfigCommandHandlerTests
{
    private static ConfigCommandHandler Build(IGodotGeneratorApiService api) =>
        new(api, NullLogger<ConfigCommandHandler>.Instance);

    private static CommandEnvelope Envelope() =>
        new(Guid.NewGuid().ToString(), ConfigCommandNames.GetAll, null);

    [Fact]
    public void CommandNames_ContainsGetAll()
    {
        var handler = Build(new Mock<IGodotGeneratorApiService>().Object);
        Assert.Contains(ConfigCommandNames.GetAll, handler.CommandNames);
    }

    [Fact]
    public async Task HandleAsync_Success_ReturnsSuccessEnvelope()
    {
        var data = new Dictionary<string, object?> { ["preferences"] = null };
        var api = new Mock<IGodotGeneratorApiService>();
        api.Setup(a => a.GetAllConfigAsync(It.IsAny<CancellationToken>()))
           .ReturnsAsync(ApiResponse<Dictionary<string, object?>>.Ok(data));

        var handler = Build(api.Object);
        var result = await handler.HandleAsync(Envelope(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.PayloadJson);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public async Task HandleAsync_ApiFailure_ReturnsHandlerFaultedError()
    {
        var api = new Mock<IGodotGeneratorApiService>();
        api.Setup(a => a.GetAllConfigAsync(It.IsAny<CancellationToken>()))
           .ReturnsAsync(ApiResponse<Dictionary<string, object?>>.Fail("upstream error"));

        var handler = Build(api.Object);
        var result = await handler.HandleAsync(Envelope(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(DesktopErrorCode.HandlerFaulted, result.ErrorCode);
    }

    [Fact]
    public async Task HandleAsync_CorrelationIdPreserved()
    {
        const string id = "cfg-corr-1";
        var api = new Mock<IGodotGeneratorApiService>();
        api.Setup(a => a.GetAllConfigAsync(It.IsAny<CancellationToken>()))
           .ReturnsAsync(ApiResponse<Dictionary<string, object?>>.Ok([]));

        var handler = Build(api.Object);
        var result = await handler.HandleAsync(new CommandEnvelope(id, ConfigCommandNames.GetAll, null), CancellationToken.None);

        Assert.Equal(id, result.CorrelationId);
    }
}
