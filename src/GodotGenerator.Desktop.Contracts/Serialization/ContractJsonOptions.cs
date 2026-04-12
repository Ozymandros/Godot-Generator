namespace GodotGenerator.Desktop.Contracts.Serialization;

/// <summary>
/// Centralised <see cref="JsonSerializerOptions"/> shared by every layer that reads or writes
/// IPC contract envelopes. Using a single instance avoids per-call metadata caching overhead
/// and guarantees consistent wire format across the pipe host, the Electron broker, and
/// the Blazor client transport.
/// </summary>
public static class ContractJsonOptions
{
    /// <summary>
    /// Default options: camelCase property names, explicit nulls included, no trailing commas,
    /// max object depth of 32, and comment handling skipped.
    /// </summary>
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = false,
        MaxDepth = 32,
        PropertyNameCaseInsensitive = true,
    };
}
