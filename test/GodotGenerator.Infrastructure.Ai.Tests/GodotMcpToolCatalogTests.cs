#nullable enable
using GodotGenerator.Infrastructure.Ai.KernelFactory;
using GodotGenerator.Infrastructure.Ai.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using Moq;
using Xunit;

namespace GodotGenerator.Infrastructure.Ai.Tests;

/// <summary>
/// Unit tests for <see cref="GodotMcpToolCatalog"/>.
/// </summary>
public sealed class GodotMcpToolCatalogTests
{
    /// <summary>
    /// Verifies an empty kernel yields no registered tool names.
    /// </summary>
    [Fact]
    public async Task ListRegisteredToolNamesAsync_returns_empty_when_kernel_has_no_functions()
    {
        var kernel = Kernel.CreateBuilder().Build();
        var factory = new Mock<IKernelFactory>();
        factory
            .Setup(f => f.GetOrCreateKernelAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(kernel);

        var sut = new GodotMcpToolCatalog(factory.Object, NullLogger<GodotMcpToolCatalog>.Instance);
        var names = await sut.ListRegisteredToolNamesAsync();

        Assert.Empty(names);
    }

    /// <summary>
    /// Verifies factory failures are logged and surfaced as an empty list.
    /// </summary>
    [Fact]
    public async Task ListRegisteredToolNamesAsync_returns_empty_on_kernel_failure()
    {
        var factory = new Mock<IKernelFactory>();
        factory
            .Setup(f => f.GetOrCreateKernelAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("mcp unavailable"));

        var sut = new GodotMcpToolCatalog(factory.Object, NullLogger<GodotMcpToolCatalog>.Instance);
        var names = await sut.ListRegisteredToolNamesAsync();

        Assert.Empty(names);
    }
}
