using GodotGenerator.Api.Abstractions;
using GodotGenerator.Api.Dtos;
using GodotGenerator.Application.Dtos;
using GodotGenerator.Blazor.Infrastructure.DesktopIpc.Handlers;
using GodotGenerator.Desktop.Contracts.Commands;
using GodotGenerator.Desktop.Contracts.Envelope;
using GodotGenerator.Desktop.Contracts.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace GodotGenerator.Desktop.Ipc.Tests.Handlers;

public sealed class GenerateCommandHandlerTests
{
    private static GenerateCommandHandler Build(IGodotGeneratorApiService api)
    {
        var services = new ServiceCollection();
        services.AddSingleton(api);
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        return new(scopeFactory, NullLogger<GenerateCommandHandler>.Instance);
    }

    private static string Json<T>(T obj) =>
        System.Text.Json.JsonSerializer.Serialize(obj, ContractJsonOptions.Default);

    private static ApiResponse<Dictionary<string, object?>> OkResult() =>
        ApiResponse<Dictionary<string, object?>>.Ok(
            new Dictionary<string, object?> { ["message"] = "done" });

    [Fact]
    public void CommandNames_ContainsAllGenerateCommands()
    {
        var h = Build(new Mock<IGodotGeneratorApiService>().Object);
        foreach (var cmd in GenerateCommandNames.All)
        {
            Assert.Contains(cmd, h.CommandNames);
        }
    }

    [Theory]
    [InlineData(GenerateCommandNames.Text)]
    [InlineData(GenerateCommandNames.Code)]
    [InlineData(GenerateCommandNames.Image)]
    [InlineData(GenerateCommandNames.Audio)]
    [InlineData(GenerateCommandNames.Video)]
    [InlineData(GenerateCommandNames.Sprites)]
    [InlineData(GenerateCommandNames.GodotUi)]
    [InlineData(GenerateCommandNames.GodotPhysics)]
    [InlineData(GenerateCommandNames.GodotProject)]
    [InlineData(GenerateCommandNames.Scenes)]
    [InlineData(GenerateCommandNames.Animations)]
    [InlineData(GenerateCommandNames.GodotLighting)]
    [InlineData(GenerateCommandNames.GodotCamera)]
    [InlineData(GenerateCommandNames.GodotShaders)]
    [InlineData(GenerateCommandNames.GodotSignals)]
    [InlineData(GenerateCommandNames.GodotNodes)]
    [InlineData(GenerateCommandNames.Wizard)]
    public async Task HandleAsync_ValidCommand_ReturnsSuccess(string command)
    {
        var api = new Mock<IGodotGeneratorApiService>();
        api.Setup(a => a.GenerateTextAsync(It.IsAny<GenerateRequest>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(OkResult());
        api.Setup(a => a.GenerateCodeAsync(It.IsAny<GenerateRequest>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(OkResult());
        api.Setup(a => a.GenerateImageAsync(It.IsAny<GenerateRequest>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(OkResult());
        api.Setup(a => a.GenerateAudioAsync(It.IsAny<GenerateRequest>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(OkResult());
        api.Setup(a => a.GenerateVideoAsync(It.IsAny<GenerateRequest>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(OkResult());
        api.Setup(a => a.GenerateSpritesAsync(It.IsAny<GenerateRequest>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(OkResult());
        api.Setup(a => a.GenerateGodotUiAsync(It.IsAny<GenerateRequest>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(OkResult());
        api.Setup(a => a.GenerateGodotPhysicsAsync(It.IsAny<GenerateRequest>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(OkResult());
        api.Setup(a => a.GenerateGodotProjectAsync(It.IsAny<GenerateRequest>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(OkResult());
        api.Setup(a => a.CreateSceneAsync(It.IsAny<GenerateRequest>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(OkResult());
        api.Setup(a => a.GenerateAnimationsAsync(It.IsAny<GenerateRequest>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(OkResult());
        api.Setup(a => a.GenerateGodotLightingAsync(It.IsAny<GenerateRequest>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(OkResult());
        api.Setup(a => a.GenerateGodotCameraAsync(It.IsAny<GenerateRequest>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(OkResult());
        api.Setup(a => a.GenerateGodotShadersAsync(It.IsAny<GenerateRequest>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(OkResult());
        api.Setup(a => a.GenerateGodotSignalsAsync(It.IsAny<GenerateRequest>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(OkResult());
        api.Setup(a => a.GenerateGodotNodesAsync(It.IsAny<GenerateRequest>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(OkResult());
        api.Setup(a => a.RunWizardAsync(It.IsAny<WizardRequest>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(OkResult());

        var h = Build(api.Object);
        var env = new CommandEnvelope("id", command,
                      Json(new GenerateCommandRequest("make something cool")));
        var result = await h.HandleAsync(env, CancellationToken.None);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task HandleAsync_MissingPayload_ReturnsValidationError()
    {
        var h = Build(new Mock<IGodotGeneratorApiService>().Object);
        var env = new CommandEnvelope("id", GenerateCommandNames.Text, null);
        var result = await h.HandleAsync(env, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(DesktopErrorCode.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task HandleAsync_EmptyPrompt_ReturnsValidationError()
    {
        var h = Build(new Mock<IGodotGeneratorApiService>().Object);
        var env = new CommandEnvelope("id", GenerateCommandNames.Text,
                      Json(new GenerateCommandRequest("")));
        var result = await h.HandleAsync(env, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(DesktopErrorCode.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task HandleAsync_ApiFailure_ReturnsHandlerFaulted()
    {
        var api = new Mock<IGodotGeneratorApiService>();
        api.Setup(a => a.GenerateTextAsync(It.IsAny<GenerateRequest>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(ApiResponse<Dictionary<string, object?>>.Fail("ai error"));

        var h = Build(api.Object);
        var env = new CommandEnvelope("id", GenerateCommandNames.Text,
                      Json(new GenerateCommandRequest("test prompt")));
        var result = await h.HandleAsync(env, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(DesktopErrorCode.HandlerFaulted, result.ErrorCode);
    }
}
