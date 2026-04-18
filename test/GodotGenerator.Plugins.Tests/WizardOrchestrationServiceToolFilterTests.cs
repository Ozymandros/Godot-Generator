#nullable enable
using GodotGenerator.Application.Dtos;
using GodotGenerator.Infrastructure.Ai.KernelFactory;
using GodotGenerator.Infrastructure.Ai.Options;
using GodotGenerator.Plugins.Plugins;
using GodotGenerator.Plugins.Services;
using GodotGenerator.Wizard.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Moq;
using Xunit;

namespace GodotGenerator.Plugins.Tests;

public sealed class WizardOrchestrationServiceToolFilterTests
{
    [Fact]
    public async Task RunAsync_injects_project_root_into_runtime_wizard_tool_invocation()
    {
        var capturedProjectRoot = string.Empty;
        var tool = KernelFunctionFactory.CreateFromMethod(
            (string projectPath) =>
            {
                capturedProjectRoot = projectPath;
                return "ok";
            },
            functionName: "godot_get_server_info");
        var plugin = KernelPluginFactory.CreateFromFunctions("godot", [tool]);

        var chat = new Mock<IChatCompletionService>();
        chat.Setup(x => x.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                It.IsAny<Kernel>(),
                It.IsAny<CancellationToken>()))
            .Returns<ChatHistory, PromptExecutionSettings, Kernel, CancellationToken>(async (_, _, kernel, ct) =>
            {
                var args = new KernelArguments
                {
                    ["projectPath"] = "",
                };
                await kernel.InvokeAsync("godot", "godot_get_server_info", args, ct).ConfigureAwait(false);
                return [new ChatMessageContent(AuthorRole.Assistant, "wizard-done")];
            });

        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton(chat.Object);
        var baseKernel = builder.Build();
        baseKernel.Plugins.Add(plugin);

        var kernelFactory = new Mock<IKernelFactory>();
        kernelFactory
            .Setup(x => x.GetOrCreateKernelAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(baseKernel);

        var gateway = new Mock<IWizardGenerationGateway>();
        gateway.Setup(x => x.GetConfigurationAsync(It.IsAny<CancellationToken>())).ReturnsAsync("ok");
        gateway.Setup(x => x.SetPreferenceAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync("ok");
        var services = new ServiceCollection();
        services.AddScoped(_ => gateway.Object);
        var provider = services.BuildServiceProvider();
        var wizardPlugin = new WizardMcpPlugin(provider.GetRequiredService<IServiceScopeFactory>());

        var sut = new WizardOrchestrationService(
            wizardPlugin,
            kernelFactory.Object,
            Microsoft.Extensions.Options.Options.Create(new OrchestrationOptions()),
            NullLogger<WizardOrchestrationService>.Instance);

        var request = new WizardRequest(
            Prompt: "validate cameras",
            ProjectName: "MyGame",
            GodotProjectPath: @"C:\MyGame");
        var result = await sut.RunAsync(request);

        Assert.True(result.Success);
        Assert.Equal(@"C:\MyGame", capturedProjectRoot);
    }
}
