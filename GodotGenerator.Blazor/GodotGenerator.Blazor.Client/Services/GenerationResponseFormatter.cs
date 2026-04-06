namespace GodotGenerator.Blazor.Client.Services;

/// <summary>
/// Normalizes <see cref="ApiResponse{T}"/> payload dictionaries from generation endpoints.
/// </summary>
public static class GenerationResponseFormatter
{
    /// <summary>Extracts user-visible text from a generation success payload.</summary>
    public static string ExtractMessage(IReadOnlyDictionary<string, object?>? data)
    {
        if (data is null)
        {
            return string.Empty;
        }

        data.TryGetValue("message", out var m);
        data.TryGetValue("detail", out var d);
        var msg = m?.ToString() ?? string.Empty;
        var detail = d?.ToString();
        if (!string.IsNullOrWhiteSpace(detail))
        {
            return string.IsNullOrEmpty(msg) ? detail! : msg + Environment.NewLine + Environment.NewLine + detail;
        }

        return msg;
    }
}
