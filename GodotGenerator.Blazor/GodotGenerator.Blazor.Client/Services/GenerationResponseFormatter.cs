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

        var msg = GetFirstText(data, "message", "result", "content", "text", "output");
        var detail = GetFirstText(data, "detail", "details", "error");
        if (!string.IsNullOrWhiteSpace(detail))
        {
            return string.IsNullOrEmpty(msg) ? detail! : msg + Environment.NewLine + Environment.NewLine + detail;
        }

        if (!string.IsNullOrWhiteSpace(msg))
        {
            return msg;
        }

        // Last-resort fallback: first non-empty string value in the payload.
        foreach (var value in data.Values)
        {
            if (value is null)
            {
                continue;
            }

            var text = value.ToString();
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text.Trim();
            }
        }

        return string.Empty;
    }

    private static string GetFirstText(IReadOnlyDictionary<string, object?> data, params string[] keys)
    {
        foreach (var key in keys)
        {
            foreach (var entry in data)
            {
                if (!string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var text = entry.Value?.ToString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text.Trim();
                }
            }
        }

        return string.Empty;
    }
}
