#nullable enable

using GodotGenerator.Api.Abstractions;
using GodotGenerator.Api.Dtos;
using GodotGenerator.Mcp.Api.Services;
using GodotGenerator.Plugins.Plugins;
using GodotGenerator.Wizard.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace GodotGenerator.Plugins.Tests;

public sealed class WizardPluginPipelineTests
{
    [Fact]
    public async Task ApiWizardGenerationGateway_generate_code_returns_message_payload()
    {
        var api = new Mock<IGodotGeneratorApiService>();
        api.Setup(x => x.GenerateCodeAsync(It.IsAny<GenerateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<Dictionary<string, object?>>.Ok(
                new Dictionary<string, object?> { ["message"] = "generated code" }));

        var sut = new ApiWizardGenerationGateway(api.Object);
        var result = await sut.GenerateCodeAsync(new WizardToolRequest("create movement script", "Demo"));

        Assert.Equal("generated code", result);
        api.Verify(x => x.GenerateCodeAsync(
            It.Is<GenerateRequest>(r => r.Prompt == "create movement script" && r.ProjectName == "Demo"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ApiWizardGenerationGateway_generation_failure_returns_error_text()
    {
        var api = new Mock<IGodotGeneratorApiService>();
        api.Setup(x => x.GenerateCodeAsync(It.IsAny<GenerateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<Dictionary<string, object?>>.Fail("bad request"));

        var sut = new ApiWizardGenerationGateway(api.Object);
        var result = await sut.GenerateCodeAsync(new WizardToolRequest("x"));

        Assert.Contains("Generation failed", result, StringComparison.Ordinal);
        Assert.Contains("bad request", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WizardMcpPlugin_resolves_gateway_and_invokes_tool()
    {
        var gateway = new Mock<IWizardGenerationGateway>();
        gateway.Setup(x => x.GenerateTextAsync(It.IsAny<WizardToolRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("generated text");

        var services = new ServiceCollection();
        services.AddScoped(_ => gateway.Object);
        var provider = services.BuildServiceProvider();

        var sut = new WizardMcpPlugin(provider.GetRequiredService<IServiceScopeFactory>());
        var result = await sut.GenerateTextAsync("write npc line", "DemoProject");

        Assert.Equal("generated text", result);
        gateway.Verify(x => x.GenerateTextAsync(
            It.Is<WizardToolRequest>(r => r.Prompt == "write npc line" && r.ProjectName == "DemoProject"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
