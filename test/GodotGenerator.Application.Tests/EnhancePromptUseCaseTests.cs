#nullable enable
using GodotGenerator.Application.Abstractions;
using GodotGenerator.Application.Dtos;
using GodotGenerator.Application.UseCases;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GodotGenerator.Application.Tests;

/// <summary>
/// Unit tests for <see cref="EnhancePromptUseCase"/>.
/// </summary>
public sealed class EnhancePromptUseCaseTests
{
    /// <summary>
    /// Missing modality key must fail fast without calling the service.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExecuteAsync_missing_modality_returns_failure(string? modality)
    {
        var service = new Mock<IPromptAssistService>(MockBehavior.Strict);
        var sut = Build(service.Object);

        var result = await sut.ExecuteAsync(new PromptAssistRequest(modality!, "some prompt"));

        Assert.False(result.Success);
        Assert.NotEmpty(result.Error!);
        service.VerifyNoOtherCalls();
    }

    /// <summary>
    /// Non-empty current prompt results in <c>"improve"</c> mode being forwarded.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_with_existing_prompt_delegates_as_improve()
    {
        var service = new Mock<IPromptAssistService>();
        service.Setup(s => s.EnhanceAsync(It.IsAny<PromptAssistRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PromptAssistResult.Ok("better prompt", "improve"));

        var sut = Build(service.Object);
        var result = await sut.ExecuteAsync(new PromptAssistRequest("code", "add player movement"));

        Assert.True(result.Success);
        Assert.Equal("improve", result.Mode);
        Assert.Equal("better prompt", result.Result);
    }

    /// <summary>
    /// Empty current prompt results in <c>"sample"</c> mode being forwarded.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_with_empty_prompt_delegates_as_sample()
    {
        var service = new Mock<IPromptAssistService>();
        service.Setup(s => s.EnhanceAsync(It.IsAny<PromptAssistRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PromptAssistResult.Ok("generated sample", "sample"));

        var sut = Build(service.Object);
        var result = await sut.ExecuteAsync(new PromptAssistRequest("godot-physics", string.Empty));

        Assert.True(result.Success);
        Assert.Equal("sample", result.Mode);
    }

    /// <summary>
    /// Service failures propagate as failed results rather than exceptions.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_propagates_service_failure()
    {
        var service = new Mock<IPromptAssistService>();
        service.Setup(s => s.EnhanceAsync(It.IsAny<PromptAssistRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PromptAssistResult.Fail("API key missing"));

        var sut = Build(service.Object);
        var result = await sut.ExecuteAsync(new PromptAssistRequest("code", "test prompt"));

        Assert.False(result.Success);
        Assert.Equal("API key missing", result.Error);
    }

    /// <summary>
    /// Cancellation is propagated through the use case.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_propagates_cancellation()
    {
        var service = new Mock<IPromptAssistService>();
        service.Setup(s => s.EnhanceAsync(It.IsAny<PromptAssistRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var sut = Build(service.Object);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => sut.ExecuteAsync(new PromptAssistRequest("text", "hello"), CancellationToken.None));
    }

    private static EnhancePromptUseCase Build(IPromptAssistService service) =>
        new(service, NullLogger<EnhancePromptUseCase>.Instance);
}
