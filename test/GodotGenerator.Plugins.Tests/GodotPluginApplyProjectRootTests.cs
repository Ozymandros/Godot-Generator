#nullable enable
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GodotMcp.Core.Interfaces;
using GodotMcp.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using GodotMcp.Plugin;

namespace GodotGenerator.Plugins.Tests;

public sealed class GodotPluginApplyProjectRootTests
{
    [Fact]
    public async Task ApplyProjectRootAsync_ReDiscoversAndRegistersTools()
    {
        var mockMcpClient = new Mock<IMcpClient>();
        var mockFunctionMapper = new Mock<IFunctionMapper>();
        var mockParameterConverter = new Mock<IParameterConverter>();

        var toolParams = new Dictionary<string, McpParameterDefinition>
        {
            ["projectPath"] = new McpParameterDefinition("projectPath", "string")
        };

        var tools = new List<McpToolDefinition>
        {
            new McpToolDefinition("godot_create_godot_project", "Create Godot project", toolParams)
        };

        mockMcpClient.Setup(c => c.ApplyProjectRootAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockMcpClient.Setup(c => c.ListToolsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<McpToolDefinition>)tools);
        mockMcpClient.Setup(c => c.ConnectAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        IReadOnlyList<McpToolDefinition>? captured = null;
        mockFunctionMapper.Setup(f => f.RegisterToolsAsync(It.IsAny<IReadOnlyList<McpToolDefinition>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<McpToolDefinition>, CancellationToken>((t, ct) => captured = t)
            .Returns(Task.CompletedTask);

        var plugin = new GodotPlugin(mockMcpClient.Object, mockFunctionMapper.Object, mockParameterConverter.Object, NullLogger<GodotPlugin>.Instance);

        await plugin.ApplyProjectRootAsync(@"C:\MyGame", CancellationToken.None);

        mockMcpClient.Verify(c => c.ApplyProjectRootAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        mockMcpClient.Verify(c => c.ListToolsAsync(It.IsAny<CancellationToken>()), Times.Once);
        mockFunctionMapper.Verify(f => f.RegisterToolsAsync(It.IsAny<IReadOnlyList<McpToolDefinition>>(), It.IsAny<CancellationToken>()), Times.Once);

        Assert.NotNull(captured);
        Assert.Single(captured);
        Assert.Equal("godot_create_godot_project", captured![0].Name);
    }
}
