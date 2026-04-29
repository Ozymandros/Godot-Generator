#nullable enable

namespace GodotGenerator.Application.Orchestration;

/// <summary>
/// Well-known phase labels emitted within a wizard turn.
/// </summary>
public static class WizardProgressPhase
{
    /// <summary>High-level milestone (turn started, turn complete, error).</summary>
    public const string Status = "status";

    /// <summary>An SK plugin tool was invoked (before or after execution).</summary>
    public const string Tool = "tool";

    /// <summary>The LLM is generating the next response chunk.</summary>
    public const string Llm = "llm";
}

/// <summary>
/// A single structured progress event emitted during a wizard orchestration turn.
/// </summary>
/// <remarks>
/// Progress frames are written to the named pipe as intermediate messages before the
/// final <c>ResponseEnvelope</c>. They are forwarded by Electron to the renderer via
/// the <c>wizardProgress</c> push-event channel and displayed live in the Wizard panel.
/// </remarks>
/// <param name="Phase">One of the <see cref="WizardProgressPhase"/> constants.</param>
/// <param name="Message">Human-readable description of the current activity.</param>
/// <param name="ToolPlugin">Plugin name when <see cref="Phase"/> is <see cref="WizardProgressPhase.Tool"/>; otherwise null.</param>
/// <param name="ToolName">Function name when <see cref="Phase"/> is <see cref="WizardProgressPhase.Tool"/>; otherwise null.</param>
/// <param name="UtcTimestamp">ISO-8601 UTC timestamp; set by the emitter, never null on the wire.</param>
public sealed record WizardProgressFrame(
    string Phase,
    string Message,
    string? ToolPlugin = null,
    string? ToolName = null,
    string? UtcTimestamp = null)
{
    /// <summary>
    /// Creates a <see cref="WizardProgressPhase.Status"/> frame with the current UTC timestamp.
    /// </summary>
    /// <param name="message">Human-readable status description.</param>
    /// <returns>A new status progress frame.</returns>
    public static WizardProgressFrame Status(string message) =>
        new(WizardProgressPhase.Status, message, UtcTimestamp: DateTime.UtcNow.ToString("O"));

    /// <summary>
    /// Creates a <see cref="WizardProgressPhase.Tool"/> frame for a specific SK function invocation.
    /// </summary>
    /// <param name="pluginName">Plugin owning the function.</param>
    /// <param name="functionName">Function (tool) being invoked.</param>
    /// <returns>A new tool-invocation progress frame.</returns>
    public static WizardProgressFrame Tool(string pluginName, string functionName) =>
        new(WizardProgressPhase.Tool,
            $"Invoking {pluginName}.{functionName}",
            ToolPlugin: pluginName,
            ToolName: functionName,
            UtcTimestamp: DateTime.UtcNow.ToString("O"));
}
