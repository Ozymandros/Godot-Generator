#nullable enable
using System.Collections.Generic;
using System.IO;
using GodotGenerator.Application.Orchestration;
using GodotGenerator.Application.Dtos;
using GodotGenerator.Infrastructure.Ai.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using GodotGenerator.Infrastructure.Ai.KernelFactory;
using GodotGenerator.Application.Abstractions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Moq;
using Xunit;

namespace GodotGenerator.Infrastructure.Ai.Tests;

public sealed class AutoInvokeRawContentTests
{
    [Fact]
    public async Task RunTurn_auto_invokes_discovered_function_with_rawContent_param()
    {
        string? captured = null;

        var tool = KernelFunctionFactory.CreateFromMethod(
            (string rawContent) =>
            {
                captured = rawContent;
                return "ok";
            },
            functionName: "godot_create_script");

        var plugin = KernelPluginFactory.CreateFromFunctions("godot", [tool]);

        var chat = new Mock<IChatCompletionService>();
        chat.Setup(x => x.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                It.IsAny<Kernel>(),
                It.IsAny<CancellationToken>()))
            .Returns<ChatHistory, PromptExecutionSettings, Kernel, CancellationToken>(async (_, _, kernel, ct) =>
            {
                return [new ChatMessageContent(AuthorRole.Assistant, "```gdscript\nextends Node\nfunc _ready():\n    pass\n```")];
            });

        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton(chat.Object);
        var kernel = builder.Build();
        kernel.Plugins.Add(plugin);

        var kernelFactory = new Mock<IKernelFactory>();
        kernelFactory.Setup(x => x.GetOrCreateKernelAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(kernel);

        var router = new Mock<IProviderCapabilityRouter>();
        string? reason = null;
        router.Setup(r => r.Supports(It.IsAny<string?>(), It.IsAny<string?>(), out reason))
            .Returns(true);

        var sut = new AiOrchestrationService(
            kernelFactory.Object,
            router.Object,
            Microsoft.Extensions.Options.Options.Create(new GodotGenerator.Infrastructure.Ai.Options.OrchestrationOptions()),
            Mock.Of<IGodotProjectPathValidator>(),
            NullLogger<AiOrchestrationService>.Instance);

        var request = new AgentTurnRequest(Prompt: "create script", Options: new Dictionary<string, object?>());

        var result = await sut.RunTurnAsync(request);

        Assert.True(result.Success);
        Assert.NotNull(captured);
        Assert.Contains("extends Node", captured);
    }
}
