#nullable enable

namespace GodotGenerator.Application.Dtos;

/// <summary>
/// Result of a prompt-assist operation.
/// </summary>
/// <param name="Success">Whether the call succeeded.</param>
/// <param name="Result">The improved or generated sample prompt text.</param>
/// <param name="Mode">
/// <c>"improve"</c> when an existing prompt was enhanced;
/// <c>"sample"</c> when a new sample was generated.
/// </param>
/// <param name="Error">User-safe error message when <see cref="Success"/> is <c>false</c>.</param>
public sealed record PromptAssistResult(
    bool Success,
    string Result,
    string Mode,
    string? Error = null)
{
    /// <summary>Creates a successful result.</summary>
    public static PromptAssistResult Ok(string result, string mode) =>
        new(true, result, mode);

    /// <summary>Creates a failure result.</summary>
    public static PromptAssistResult Fail(string error, string mode = "improve") =>
        new(false, string.Empty, mode, error);
}
