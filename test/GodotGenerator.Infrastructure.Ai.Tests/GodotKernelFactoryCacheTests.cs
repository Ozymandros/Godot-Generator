#nullable enable
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GodotGenerator.Infrastructure.Ai.KernelFactory;
using GodotGenerator.Infrastructure.Ai.Options;
using GodotGenerator.Infrastructure.Ai.Services;
using GodotMcp.Core.Interfaces;
using GodotMcp.Plugin.Mapping;
using GodotMcp.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using GodotMcp.Plugin;

namespace GodotGenerator.Infrastructure.Ai.Tests;

public sealed class GodotKernelFactoryCacheTests
{
    [Fact]
    public async Task GetOrCreateKernelAsync_DifferentProjectRootsProduceDifferentKernels()
    {
        var providerResolver = new Mock<IProviderConnectionResolver>();
        providerResolver
            .Setup(p => p.ResolveAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderConnectionSettings("testProvider", "testModel", "apikey", null));

        var mockMcpClient = new Mock<IMcpClient>();
        mockMcpClient.Setup(c => c.ConnectAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var tools = new List<McpToolDefinition>
        {
            new McpToolDefinition("godot_create_godot_project", "desc", new Dictionary<string, McpParameterDefinition>())
        };
        mockMcpClient.Setup(c => c.ListToolsAsync(It.IsAny<CancellationToken>())).ReturnsAsync((IReadOnlyList<McpToolDefinition>)tools);
        ///*mockMcpClient.Setup(c => c.ApplyProjectRootAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>())).Returns*/(Task.CompletedTask);

        var functionMapper = new FunctionMapper(NullLogger<FunctionMapper>.Instance);
        var parameterConverter = new Mock<IParameterConverter>();
        var godotPlugin = new GodotPlugin(mockMcpClient.Object, functionMapper, parameterConverter.Object, NullLogger<GodotPlugin>.Instance);

        var services = new ServiceCollection();
        services.AddSingleton<GodotPlugin>(godotPlugin);
        services.AddSingleton<IFunctionMapper>(functionMapper);
        var serviceProvider = services.BuildServiceProvider();

        var loggerFactory = new LoggerFactory();
        var logger = loggerFactory.CreateLogger<GodotKernelFactory>();
        var factory = new GodotKernelFactory(serviceProvider, Microsoft.Extensions.Options.Options.Create(new OrchestrationOptions()), providerResolver.Object, loggerFactory, logger);

        var k1 = await factory.GetOrCreateKernelAsync(null, null, null, @"C:\ProjectA", CancellationToken.None);
        var k2 = await factory.GetOrCreateKernelAsync(null, null, null, @"C:\ProjectA", CancellationToken.None);
        var k3 = await factory.GetOrCreateKernelAsync(null, null, null, @"C:\ProjectB", CancellationToken.None);

        Assert.Same(k1, k2);
        Assert.NotSame(k1, k3);
    }
}
