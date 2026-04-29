#nullable enable

namespace GodotGenerator.Application.Orchestration;

/// <summary>
/// Per-async-flow sink for wizard IPC progress frames.
/// Mirrors the <c>GodotSkTurnContext</c> pattern: because the wizard kernel is
/// shared/cached across turns, request-scoped state must live in
/// <see cref="System.Threading.AsyncLocal{T}"/> so it is tied to the current turn
/// without polluting global state.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Producer side</strong> (<c>WizardOrchestrationService</c>): call
/// <see cref="EmitIfActive"/> anywhere inside the wizard turn. If no sink is
/// installed (e.g. HTTP path, unit tests), the call is a no-op.
/// </para>
/// <para>
/// <strong>Consumer side</strong> (<c>NamedPipeCommandHost</c>): call
/// <see cref="Enter"/> before dispatching the wizard command and dispose the
/// returned scope in a <c>finally</c> block.  The scope clears the sink so
/// subsequent (unrelated) async work is not polluted.
/// </para>
/// </remarks>
public static class WizardIpcProgressContext
{
    private static readonly AsyncLocal<Action<WizardProgressFrame>?> _current = new();

    /// <summary>
    /// Installs <paramref name="sink"/> as the active progress handler for the current
    /// async-execution context and all child continuations.
    /// </summary>
    /// <param name="sink">
    /// Callback invoked on each <see cref="WizardProgressFrame"/>.
    /// Must be thread-safe; may be called concurrently from different async continuations.
    /// </param>
    /// <returns>
    /// A disposable scope that clears the sink when disposed.
    /// Always dispose in a <c>finally</c> block.
    /// </returns>
    public static IDisposable Enter(Action<WizardProgressFrame> sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        _current.Value = sink;
        return new ClearScope();
    }

    /// <summary>
    /// Gets the currently active progress sink, or <see langword="null"/> when no wizard
    /// turn has installed a sink on this async flow (e.g. HTTP path or unit tests).
    /// </summary>
    public static Action<WizardProgressFrame>? Current => _current.Value;

    /// <summary>
    /// Emits <paramref name="frame"/> to the active sink if one is installed; otherwise
    /// does nothing.  This is the preferred call site for producers.
    /// </summary>
    /// <param name="frame">The progress frame to emit.</param>
    public static void EmitIfActive(WizardProgressFrame frame) =>
        _current.Value?.Invoke(frame);

    // ── Inner scope ──────────────────────────────────────────────────────────

    private sealed class ClearScope : IDisposable
    {
        /// <summary>Clears the progress sink from the current async context.</summary>
        public void Dispose() => _current.Value = null;
    }
}
