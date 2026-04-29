#nullable enable
using System.Collections.Generic;
using System.IO;
using GodotGenerator.Application.Abstractions;
using GodotGenerator.Application.Dtos;
using GodotGenerator.Application.Orchestration;
using GodotGenerator.Infrastructure.Ai.KernelFactory;
using GodotGenerator.Infrastructure.Ai.Options;
using GodotGenerator.Infrastructure.Ai.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Moq;
using Xunit;

namespace GodotGenerator.Infrastructure.Ai.Tests;

public sealed class CreateProjectInjectionTests
{
    [Fact]
    public async Task RunTurn_injects_composite_path_for_create_project()
    {
        var capturedDirectory = string.Empty;
        var capturedName = string.Empty;

        var tool = KernelFunctionFactory.CreateFromMethod(
            (string directory, string name) =>
            {
                capturedDirectory = directory;
                capturedName = name;
                return "ok";
            },
            functionName: "godot_create_godot_project");

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
                    ["directory"] = ".",
                };
                await kernel.InvokeAsync("godot", "godot_create_godot_project", args, ct).ConfigureAwait(false);
                return [new ChatMessageContent(AuthorRole.Assistant, "done")];
            });

        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton(chat.Object);
        var kernel = builder.Build();
        kernel.Plugins.Add(plugin);

        var kernelFactory = new Mock<IKernelFactory>();
        kernelFactory.Setup(x => x.GetOrCreateKernelAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(kernel);

        var validator = new Mock<IGodotProjectPathValidator>();
        validator.Setup(v => v.IsValidGodotProjectRootAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var router = new Mock<IProviderCapabilityRouter>();
        string? reason = null;
        router.Setup(r => r.Supports(It.IsAny<string?>(), It.IsAny<string?>(), out reason))
            .Returns(true);

        var sut = new AiOrchestrationService(
            kernelFactory.Object,
            router.Object,
            Microsoft.Extensions.Options.Options.Create(new OrchestrationOptions()),
            validator.Object,
            NullLogger<AiOrchestrationService>.Instance);

        var request = new AgentTurnRequest(
            Prompt: "create project",
            Options: new Dictionary<string, object?>
            {
                [ModalityTurnComposer.GodotProjectPathOptionKey] = @"C:\MyGame",
                [ModalityTurnComposer.ProjectNameOptionKey] = "MyGame",
            });

        var result = await sut.RunTurnAsync(request);

        Assert.True(result.Success);
        Assert.Equal(Path.Combine(@"C:\MyGame", "MyGame"), capturedDirectory);
        Assert.Equal("MyGame", capturedName);
    }
}
