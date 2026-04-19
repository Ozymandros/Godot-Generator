#nullable enable
using System.Collections.Generic;
using GodotGenerator.Application.Abstractions;
using GodotGenerator.Application.Dtos;
using GodotGenerator.Application.Orchestration;
using GodotGenerator.Infrastructure.Ai.KernelFactory;
using GodotGenerator.Infrastructure.Ai.Options;
using GodotGenerator.Infrastructure.Ai.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GodotGenerator.Infrastructure.Ai.Tests;

/// <summary>
/// Unit tests for optional Godot project path validation in <see cref="AiOrchestrationService"/>.
/// </summary>
public sealed class AiOrchestrationValidationTests
{
    /// <summary>
    /// Verifies invalid project path short-circuits before kernel creation.
    /// </summary>
    [Fact]
    public async Task RunTurnAsync_fails_when_godot_project_path_invalid()
    {
        var factory = new Mock<IKernelFactory>(MockBehavior.Strict);
        var validator = new Mock<IGodotProjectPathValidator>();
        validator
            .Setup(v => v.IsValidGodotProjectRootAsync("bad", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var sut = new AiOrchestrationService(
            factory.Object,
            CreateSupportedRouter().Object,
            Microsoft.Extensions.Options.Options.Create(new OrchestrationOptions()),
            validator.Object,
            NullLogger<AiOrchestrationService>.Instance);

        var request = new AgentTurnRequest(
            "hi",
            Options: new Dictionary<string, object?>
            {
                [ModalityTurnComposer.GodotProjectPathOptionKey] = "bad",
            });

        var result = await sut.RunTurnAsync(request);

        Assert.False(result.Success);
        factory.Verify(
            f => f.GetOrCreateKernelAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Verifies valid project path continues to orchestration.
    /// </summary>
    [Fact]
    public async Task RunTurnAsync_proceeds_when_godot_project_path_valid()
    {
        var factory = new Mock<IKernelFactory>();
        factory
            .Setup(f => f.GetOrCreateKernelAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("kernel unavailable"));

        var validator = new Mock<IGodotProjectPathValidator>();
        validator
            .Setup(v => v.IsValidGodotProjectRootAsync("good", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var sut = new AiOrchestrationService(
            factory.Object,
            CreateSupportedRouter().Object,
            Microsoft.Extensions.Options.Options.Create(new OrchestrationOptions()),
            validator.Object,
            NullLogger<AiOrchestrationService>.Instance);

        var request = new AgentTurnRequest(
            "hi",
            Options: new Dictionary<string, object?>
            {
                [ModalityTurnComposer.GodotProjectPathOptionKey] = "good",
            });

        var result = await sut.RunTurnAsync(request);

        Assert.False(result.Success);
        factory.Verify(
            f => f.GetOrCreateKernelAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static Mock<IProviderCapabilityRouter> CreateSupportedRouter()
    {
        var router = new Mock<IProviderCapabilityRouter>();
        string? reason = null;
        router.Setup(r => r.Supports(It.IsAny<string?>(), It.IsAny<string?>(), out reason))
            .Returns(true);
        return router;
    }
}
