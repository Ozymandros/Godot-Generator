using GodotGenerator.Blazor.Infrastructure.DesktopIpc;
using GodotGenerator.Desktop.Contracts.Envelope;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace GodotGenerator.Desktop.Ipc.Tests;

public sealed class CommandDispatcherTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static CommandDispatcher BuildDispatcher(params ICommandHandler[] handlers) =>
        new(handlers, NullLogger<CommandDispatcher>.Instance);

    private static CommandEnvelope Envelope(string command, string? payload = null) =>
        new(Guid.NewGuid().ToString(), command, payload);

    // ── Route-to-handler ──────────────────────────────────────────────────────

    [Fact]
    public async Task DispatchAsync_KnownCommand_CallsRegisteredHandler()
    {
        var handler = new Mock<ICommandHandler>();
        handler.SetupGet(h => h.CommandNames).Returns(["test.cmd"]);
        var expected = new ResponseEnvelope("id", true, null, null, null);
        handler.Setup(h => h.HandleAsync(It.IsAny<CommandEnvelope>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(expected);

        var dispatcher = BuildDispatcher(handler.Object);
        var result = await dispatcher.DispatchAsync(Envelope("test.cmd"), CancellationToken.None);

        Assert.True(result.Success);
        handler.Verify(h => h.HandleAsync(It.IsAny<CommandEnvelope>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_UnknownCommand_ReturnsUnknownCommandError()
    {
        var dispatcher = BuildDispatcher(); // no handlers
        var result = await dispatcher.DispatchAsync(Envelope("no.such.command"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(DesktopErrorCode.UnknownCommand, result.ErrorCode);
    }

    // ── Error mapping ─────────────────────────────────────────────────────────

    [Fact]
    public async Task DispatchAsync_HandlerThrows_ReturnsHandlerFaulted()
    {
        var handler = new Mock<ICommandHandler>();
        handler.SetupGet(h => h.CommandNames).Returns(["throws.cmd"]);
        handler.Setup(h => h.HandleAsync(It.IsAny<CommandEnvelope>(), It.IsAny<CancellationToken>()))
               .ThrowsAsync(new InvalidOperationException("boom"));

        var dispatcher = BuildDispatcher(handler.Object);
        var result = await dispatcher.DispatchAsync(Envelope("throws.cmd"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(DesktopErrorCode.HandlerFaulted, result.ErrorCode);
    }

    [Fact]
    public async Task DispatchAsync_HandlerCancelled_ReturnsTimeout()
    {
        var handler = new Mock<ICommandHandler>();
        handler.SetupGet(h => h.CommandNames).Returns(["cancel.cmd"]);
        handler.Setup(h => h.HandleAsync(It.IsAny<CommandEnvelope>(), It.IsAny<CancellationToken>()))
               .ThrowsAsync(new OperationCanceledException());

        var dispatcher = BuildDispatcher(handler.Object);
        var result = await dispatcher.DispatchAsync(Envelope("cancel.cmd"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(DesktopErrorCode.Timeout, result.ErrorCode);
    }

    // ── Multi-command handlers ────────────────────────────────────────────────

    [Fact]
    public async Task DispatchAsync_HandlerExposesMultipleCommands_AllRouteCorrectly()
    {
        var handler = new Mock<ICommandHandler>();
        handler.SetupGet(h => h.CommandNames).Returns(["cmd.a", "cmd.b"]);
        handler.Setup(h => h.HandleAsync(It.IsAny<CommandEnvelope>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync((CommandEnvelope e, CancellationToken _) =>
                   new ResponseEnvelope(e.CorrelationId, true, null, null, null));

        var dispatcher = BuildDispatcher(handler.Object);

        var resultA = await dispatcher.DispatchAsync(Envelope("cmd.a"), CancellationToken.None);
        var resultB = await dispatcher.DispatchAsync(Envelope("cmd.b"), CancellationToken.None);

        Assert.True(resultA.Success);
        Assert.True(resultB.Success);
    }

    // ── Correlation id propagation ────────────────────────────────────────────

    [Fact]
    public async Task DispatchAsync_PreservesCorrelationId()
    {
        const string correlationId = "test-correlation-42";
        var handler = new Mock<ICommandHandler>();
        handler.SetupGet(h => h.CommandNames).Returns(["echo.cmd"]);
        handler.Setup(h => h.HandleAsync(It.IsAny<CommandEnvelope>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync((CommandEnvelope e, CancellationToken _) =>
                   new ResponseEnvelope(e.CorrelationId, true, null, null, null));

        var dispatcher = BuildDispatcher(handler.Object);
        var result = await dispatcher.DispatchAsync(
            new CommandEnvelope(correlationId, "echo.cmd", null), CancellationToken.None);

        Assert.Equal(correlationId, result.CorrelationId);
    }

    // ── Registered names ─────────────────────────────────────────────────────

    [Fact]
    public void RegisteredCommandNames_ContainsAllHandlerNames()
    {
        var h1 = new Mock<ICommandHandler>();
        h1.SetupGet(h => h.CommandNames).Returns(["a", "b"]);
        var h2 = new Mock<ICommandHandler>();
        h2.SetupGet(h => h.CommandNames).Returns(["c"]);

        var dispatcher = BuildDispatcher(h1.Object, h2.Object);

        Assert.Contains("a", dispatcher.RegisteredCommandNames);
        Assert.Contains("b", dispatcher.RegisteredCommandNames);
        Assert.Contains("c", dispatcher.RegisteredCommandNames);
    }
}
