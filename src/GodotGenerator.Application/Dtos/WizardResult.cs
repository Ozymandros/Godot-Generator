#nullable enable

namespace GodotGenerator.Application.Dtos;

/// <summary>
/// Result of a Wizard orchestration turn, including the LLM's final response and a
/// summary of which generation tools were invoked during the turn.
/// </summary>
/// <param name="Success">Whether the turn completed without a fatal error.</param>
/// <param name="Message">
/// The LLM's final narrative response, typically a summary of everything that was generated.
/// </param>
/// <param name="ToolsInvoked">
/// Names of the SK kernel functions called by the LLM during this turn.
/// May be empty if the LLM answered without calling tools.
/// </param>
/// <param name="Error">User-safe error description when <see cref="Success"/> is <c>false</c>.</param>
public sealed record WizardResult(
    bool Success,
    string Message,
    IReadOnlyList<string> ToolsInvoked,
    string? Error = null)
{
    /// <summary>Creates a successful wizard result.</summary>
    public static WizardResult Ok(string message, IReadOnlyList<string> toolsInvoked) =>
        new(true, message, toolsInvoked);

    /// <summary>Creates a failure wizard result.</summary>
    public static WizardResult Fail(string error) =>
        new(false, string.Empty, Array.Empty<string>(), error);
}
