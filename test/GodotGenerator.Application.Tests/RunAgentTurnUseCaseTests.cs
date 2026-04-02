using GodotGenerator.Application.Abstractions;
using GodotGenerator.Application.Dtos;
using GodotGenerator.Application.UseCases;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GodotGenerator.Application.Tests;

/// <summary>
/// Unit tests for <see cref="RunAgentTurnUseCase"/>.
/// </summary>
public sealed class RunAgentTurnUseCaseTests
{
    /// <summary>
    /// Verifies that an empty prompt is rejected without calling orchestration.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_empty_prompt_returns_failure()
    {
        var ai = new Mock<IAiOrchestrationService>(MockBehavior.Strict);
        var sut = new RunAgentTurnUseCase(ai.Object, NullLogger<RunAgentTurnUseCase>.Instance);
        var result = await sut.ExecuteAsync(new AgentTurnRequest("   "));
        Assert.False(result.Success);
    }

    /// <summary>
    /// Verifies that a valid request is delegated to <see cref="IAiOrchestrationService"/>.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_delegates_to_orchestration()
    {
        var ai = new Mock<IAiOrchestrationService>();
        ai.Setup(a => a.RunTurnAsync(It.IsAny<AgentTurnRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentTurnResult(true, "ok"));
        var sut = new RunAgentTurnUseCase(ai.Object, NullLogger<RunAgentTurnUseCase>.Instance);
        var result = await sut.ExecuteAsync(new AgentTurnRequest("hello"));
        Assert.True(result.Success);
        Assert.Equal("ok", result.Message);
    }
}
