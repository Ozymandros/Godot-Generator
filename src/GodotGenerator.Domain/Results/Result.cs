#nullable enable

namespace GodotGenerator.Domain.Results;

/// <summary>
/// Represents success or failure without throwing for expected failure cases.
/// </summary>
/// <typeparam name="T">Payload type on success.</typeparam>
public readonly record struct Result<T>(T? Value, string? Error)
{
    /// <summary>
    /// Gets a value indicating whether the operation succeeded.
    /// </summary>
    public bool IsSuccess => Error is null;

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static Result<T> Ok(T value) => new(value, null);

    /// <summary>
    /// Creates a failed result with an error message.
    /// </summary>
    public static Result<T> Fail(string error) => new(default, error);
}
