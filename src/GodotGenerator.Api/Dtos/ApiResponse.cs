#nullable enable

namespace GodotGenerator.Api.Dtos;

/// <summary>
/// Standard transport-agnostic response envelope for public API-layer services.
/// </summary>
/// <typeparam name="T">Payload type.</typeparam>
public sealed record ApiResponse<T>(
    bool Success,
    DateTimeOffset Date,
    string? Error,
    T? Data)
{
    /// <summary>
    /// Creates a successful response envelope.
    /// </summary>
    /// <param name="data">Response payload.</param>
    /// <returns>Successful response envelope.</returns>
    public static ApiResponse<T> Ok(T data) => new(true, DateTimeOffset.UtcNow, null, data);

    /// <summary>
    /// Creates a failed response envelope.
    /// </summary>
    /// <param name="error">Error message.</param>
    /// <returns>Failure response envelope.</returns>
    public static ApiResponse<T> Fail(string error) => new(false, DateTimeOffset.UtcNow, error, default);
}
