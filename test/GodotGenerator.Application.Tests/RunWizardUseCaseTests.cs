#nullable enable
using GodotGenerator.Application.Abstractions;
using GodotGenerator.Application.Dtos;
using GodotGenerator.Application.UseCases;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GodotGenerator.Application.Tests;

/// <summary>
/// Unit tests for <see cref="RunWizardUseCase"/>.
/// </summary>
public sealed class RunWizardUseCaseTests
{
    /// <summary>Empty prompt is rejected before calling the orchestration service.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExecuteAsync_empty_prompt_returns_failure_without_calling_service(string? prompt)
    {
        var svc = new Mock<IWizardOrchestrationService>(MockBehavior.Strict);
        var sut = Build(svc.Object);

        var result = await sut.ExecuteAsync(new WizardRequest(prompt!));

        Assert.False(result.Success);
        Assert.NotEmpty(result.Error!);
        svc.VerifyNoOtherCalls();
    }

    /// <summary>A valid prompt is forwarded to the orchestration service and the result is returned.</summary>
    [Fact]
    public async Task ExecuteAsync_valid_prompt_delegates_and_returns_result()
    {
        var expected = WizardResult.Ok("code + scene generated", ["GodotGeneratorApi.generate_code", "GodotGeneratorApi.create_scene"]);
        var svc = new Mock<IWizardOrchestrationService>();
        svc.Setup(s => s.RunAsync(It.IsAny<WizardRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var sut = Build(svc.Object);
        var result = await sut.ExecuteAsync(new WizardRequest("Build a platformer player"));

        Assert.True(result.Success);
        Assert.Equal("code + scene generated", result.Message);
        Assert.Equal(2, result.ToolsInvoked.Count);
    }

    /// <summary>Service failures are propagated transparently.</summary>
    [Fact]
    public async Task ExecuteAsync_propagates_service_failure()
    {
        var svc = new Mock<IWizardOrchestrationService>();
        svc.Setup(s => s.RunAsync(It.IsAny<WizardRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WizardResult.Fail("API key missing"));

        var sut = Build(svc.Object);
        var result = await sut.ExecuteAsync(new WizardRequest("do something"));

        Assert.False(result.Success);
        Assert.Equal("API key missing", result.Error);
    }

    /// <summary>Cancellation is propagated through the use case.</summary>
    [Fact]
    public async Task ExecuteAsync_propagates_cancellation()
    {
        var svc = new Mock<IWizardOrchestrationService>();
        svc.Setup(s => s.RunAsync(It.IsAny<WizardRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var sut = Build(svc.Object);
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => sut.ExecuteAsync(new WizardRequest("anything")));
    }

    private static RunWizardUseCase Build(IWizardOrchestrationService svc) =>
        new(svc, NullLogger<RunWizardUseCase>.Instance);
}
