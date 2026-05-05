using System.ComponentModel.DataAnnotations;
using System.IO;

namespace GodotGenerator.Infrastructure.Persistence.Options;

/// <summary>
/// Configuration for the JSON file data store root directory.
/// </summary>
public sealed class DataStoreOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "DataStore";

    /// <summary>
    /// Root directory for JSON data files (must be writable).
    /// </summary>
    [Required]
    public string RootPath { get; set; } = ResolveDefaultRootPath();

    private static string ResolveDefaultRootPath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            return Path.Combine(localAppData, "GodotGenerator", "data");
        }

        // Cross-platform fallback when LocalApplicationData is unavailable.
        return Path.Combine(AppContext.BaseDirectory, "data");
    }
}
