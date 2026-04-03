#nullable enable
using GodotGenerator.Application.Abstractions;
using GodotGenerator.Application.Dtos;
using GodotGenerator.Infrastructure.Ai.KernelFactory;
using GodotGenerator.Infrastructure.Ai.Options;
using GodotGenerator.Infrastructure.Ai.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GodotGenerator.Infrastructure.Ai.Tests;

/// <summary>
/// Unit tests for <see cref="AiOrchestrationService"/> failure normalization behavior.
/// </summary>
public sealed class AiOrchestrationServiceTests
{
    /// <summary>
    /// Verifies that orchestration returns a safe failure result when kernel creation fails.
    /// </summary>
    [Fact]
    public async Task RunTurnAsync_returns_failure_when_kernel_factory_throws()
    {
        var factory = new Mock<IKernelFactory>();
        factory
            .Setup(f => f.GetOrCreateKernelAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("godot-mcp not available"));

        var validator = new Mock<IGodotProjectPathValidator>();
        var router = CreateSupportedRouter();
        var sut = new AiOrchestrationService(
            factory.Object,
            router.Object,
            Microsoft.Extensions.Options.Options.Create(new OrchestrationOptions()),
            validator.Object,
            NullLogger<AiOrchestrationService>.Instance);
        var result = await sut.RunTurnAsync(new AgentTurnRequest("ping"));

        Assert.False(result.Success);
        Assert.Equal("Agent turn failed. Check logs for details.", result.Message);
        Assert.Contains("godot-mcp", result.Detail ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies that configured generic failure text is returned while details are preserved in the detail field.
    /// </summary>
    [Fact]
    public async Task RunTurnAsync_returns_custom_safe_message_when_configured()
    {
        var factory = new Mock<IKernelFactory>();
        factory
            .Setup(f => f.GetOrCreateKernelAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("underlying details"));

        var validator = new Mock<IGodotProjectPathValidator>();
        var router = CreateSupportedRouter();
        var sut = new AiOrchestrationService(
            factory.Object,
            router.Object,
            Microsoft.Extensions.Options.Options.Create(new OrchestrationOptions { GenericFailureMessage = "Something went wrong." }),
            validator.Object,
            NullLogger<AiOrchestrationService>.Instance);

        var result = await sut.RunTurnAsync(new AgentTurnRequest("ping", PreferredModelId: "gpt-4o"));

        Assert.False(result.Success);
        Assert.Equal("Something went wrong.", result.Message);
        Assert.Contains("underlying details", result.Detail ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        factory.Verify(
            f => f.GetOrCreateKernelAsync(null, "gpt-4o", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Verifies that long internal error details are truncated to a bounded length.
    /// </summary>
    [Fact]
    public async Task RunTurnAsync_truncates_detail_to_500_chars()
    {
        var longMessage = new string('x', 800);
        var factory = new Mock<IKernelFactory>();
        factory
            .Setup(f => f.GetOrCreateKernelAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(longMessage));

        var validator = new Mock<IGodotProjectPathValidator>();
        var router = CreateSupportedRouter();
        var sut = new AiOrchestrationService(
            factory.Object,
            router.Object,
            Microsoft.Extensions.Options.Options.Create(new OrchestrationOptions()),
            validator.Object,
            NullLogger<AiOrchestrationService>.Instance);

        var result = await sut.RunTurnAsync(new AgentTurnRequest("ping"));

        Assert.False(result.Success);
        Assert.NotNull(result.Detail);
        Assert.Equal(503, result.Detail!.Length);
        Assert.EndsWith("...", result.Detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies missing provider key errors are returned as actionable user-safe messages.
    /// </summary>
    [Fact]
    public async Task RunTurnAsync_returns_actionable_message_for_missing_provider_key()
    {
        var factory = new Mock<IKernelFactory>();
        factory
            .Setup(f => f.GetOrCreateKernelAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("API key for provider 'openai' is not configured. Set it in Settings > Secrets."));

        var validator = new Mock<IGodotProjectPathValidator>();
        var router = CreateSupportedRouter();
        var sut = new AiOrchestrationService(
            factory.Object,
            router.Object,
            Microsoft.Extensions.Options.Options.Create(new OrchestrationOptions()),
            validator.Object,
            NullLogger<AiOrchestrationService>.Instance);

        var result = await sut.RunTurnAsync(new AgentTurnRequest("ping"));

        Assert.False(result.Success);
        Assert.Equal("API key for provider 'openai' is not configured. Set it in Settings > Secrets.", result.Message);
    }

    [Fact]
    public async Task RunTurnAsync_returns_failure_for_unsupported_provider()
    {
        var factory = new Mock<IKernelFactory>(MockBehavior.Strict);
        var validator = new Mock<IGodotProjectPathValidator>();
        var router = new Mock<IProviderCapabilityRouter>();
        string? reason;
        router.Setup(r => r.Supports("anthropic", It.IsAny<string?>(), out reason!))
            .Returns(false);

        var sut = new AiOrchestrationService(
            factory.Object,
            router.Object,
            Microsoft.Extensions.Options.Options.Create(new OrchestrationOptions()),
            validator.Object,
            NullLogger<AiOrchestrationService>.Instance);

        var result = await sut.RunTurnAsync(new AgentTurnRequest("ping", Provider: "anthropic"));
        Assert.False(result.Success);
        Assert.Contains("Unsupported", result.Message, StringComparison.OrdinalIgnoreCase);
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
