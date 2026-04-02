using System.ComponentModel.DataAnnotations;

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
    public string RootPath { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GodotGenerator", "data");
}
