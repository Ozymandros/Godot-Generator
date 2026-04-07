using GodotGenerator.Desktop.Contracts.Envelope;

namespace GodotGenerator.Blazor.Infrastructure.DesktopIpc;

/// <summary>
/// Contract for a typed IPC command handler. Each implementation is responsible for
/// one or more versioned command names and performs the application work for those commands.
/// </summary>
internal interface ICommandHandler
{
    /// <summary>
    /// The versioned command names this handler responds to (e.g. <c>Config.GetAll/v1</c>).
    /// Multiple names may be returned when one handler covers a family of related commands.
    /// </summary>
    IReadOnlyList<string> CommandNames { get; }

    /// <summary>
    /// Executes the command described by <paramref name="envelope"/> and returns a
    /// <see cref="ResponseEnvelope"/> carrying either a success payload or a typed error.
    /// </summary>
    /// <param name="envelope">The incoming command envelope from the Electron broker.</param>
    /// <param name="ct">Cancellation token propagated from the pipe host's timeout policy.</param>
    Task<ResponseEnvelope> HandleAsync(CommandEnvelope envelope, CancellationToken ct);
}
