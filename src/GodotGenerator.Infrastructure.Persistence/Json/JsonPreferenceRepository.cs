#nullable enable
using GodotGenerator.Application.Abstractions;
using GodotGenerator.Infrastructure.Persistence.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GodotGenerator.Infrastructure.Persistence.Json;

/// <summary>
/// JSON file–backed <see cref="IPreferenceRepository"/> with atomic writes and a per-instance lock.
/// </summary>
public sealed class JsonPreferenceRepository : IPreferenceRepository, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _filePath;
    private readonly ILogger<JsonPreferenceRepository> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private const string AgentDebugLogPath = @"C:\Projects\Godot-Generator-Avalonia\debug-cb9046.log";

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonPreferenceRepository"/> class.
    /// </summary>
    public JsonPreferenceRepository(IOptions<DataStoreOptions> options, ILogger<JsonPreferenceRepository> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _logger = logger;
        var root = options.Value.RootPath;
        Directory.CreateDirectory(root);
        _filePath = Path.Combine(root, "preferences.json");
    }

    /// <inheritdoc />
    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var doc = await LoadAsync(cancellationToken).ConfigureAwait(false);
            var found = doc.Values.TryGetValue(key, out var v);
            #region agent log
            try
            {
                var line = JsonSerializer.Serialize(new
                {
                    sessionId = "cb9046",
                    runId = "initial",
                    hypothesisId = "H1_H4",
                    location = "JsonPreferenceRepository.cs:GetAsync",
                    message = "Preference read",
                    data = new
                    {
                        key,
                        found,
                        valueLength = found ? (v?.Length ?? 0) : 0,
                        filePath = _filePath
                    },
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                });
                File.AppendAllText(AgentDebugLogPath, line + Environment.NewLine);
            }
            catch
            {
                // no-op
            }
            #endregion
            return found ? v : null;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task SetAsync(string key, string? value, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var doc = await LoadAsync(cancellationToken).ConfigureAwait(false);
            #region agent log
            try
            {
                var line = JsonSerializer.Serialize(new
                {
                    sessionId = "cb9046",
                    runId = "initial",
                    hypothesisId = "H1_H4",
                    location = "JsonPreferenceRepository.cs:SetAsync",
                    message = "Preference write request",
                    data = new
                    {
                        key,
                        isNullValue = value is null,
                        valueLength = value?.Length ?? 0,
                        filePath = _filePath
                    },
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                });
                File.AppendAllText(AgentDebugLogPath, line + Environment.NewLine);
            }
            catch
            {
                // no-op
            }
            #endregion
            if (value is null)
            {
                doc.Values.Remove(key);
            }
            else
            {
                doc.Values[key] = value;
            }

            await SaveAsync(doc, cancellationToken).ConfigureAwait(false);
            #region agent log
            try
            {
                var line = JsonSerializer.Serialize(new
                {
                    sessionId = "cb9046",
                    runId = "initial",
                    hypothesisId = "H1_H4",
                    location = "JsonPreferenceRepository.cs:SetAsync",
                    message = "Preference write persisted",
                    data = new
                    {
                        key,
                        hasKeyAfterSave = doc.Values.ContainsKey(key),
                        valueLengthAfterSave = doc.Values.TryGetValue(key, out var saved) ? (saved?.Length ?? 0) : 0,
                        filePath = _filePath
                    },
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                });
                File.AppendAllText(AgentDebugLogPath, line + Environment.NewLine);
            }
            catch
            {
                // no-op
            }
            #endregion
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Loads the persisted preferences document, returning an empty document when missing or invalid.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Loaded preferences document.</returns>
    private async Task<PreferencesDocument> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            return new PreferencesDocument();
        }

        try
        {
            await using var stream = File.OpenRead(_filePath);
            var doc = await JsonSerializer.DeserializeAsync<PreferencesDocument>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
            return doc ?? new PreferencesDocument();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Preferences file is invalid JSON at {Path}. Falling back to empty preferences.", _filePath);
            return new PreferencesDocument();
        }
    }

    /// <summary>
    /// Persists a preferences document using atomic replacement semantics.
    /// </summary>
    /// <param name="doc">Document to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task SaveAsync(PreferencesDocument doc, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temp = _filePath + ".tmp";
        try
        {
            var json = JsonSerializer.Serialize(doc, JsonOptions);
            await using (var stream = File.Create(temp))
            {
                await JsonSerializer.SerializeAsync(stream, doc, JsonOptions, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            if (File.Exists(_filePath))
            {
                try
                {
                    // File.Replace may fail on Windows when the destination is held by another
                    // process without delete sharing. Use Move(overwrite) as the primary replacement path.
                    File.Move(temp, _filePath, overwrite: true);
                    #region agent log
                    try
                    {
                        var line = JsonSerializer.Serialize(new
                        {
                            sessionId = "cb9046",
                            runId = "post-fix",
                            hypothesisId = "H6",
                            location = "JsonPreferenceRepository.cs:SaveAsync",
                            message = "Atomic replace by move succeeded",
                            data = new { filePath = _filePath },
                            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                        });
                        File.AppendAllText(AgentDebugLogPath, line + Environment.NewLine);
                    }
                    catch
                    {
                        // no-op
                    }
                    #endregion
                    return;
                }
                catch (IOException moveEx)
                {
                    #region agent log
                    try
                    {
                        var line = JsonSerializer.Serialize(new
                        {
                            sessionId = "cb9046",
                            runId = "post-fix",
                            hypothesisId = "H6",
                            location = "JsonPreferenceRepository.cs:SaveAsync",
                            message = "Atomic replace by move failed; using direct write fallback",
                            data = new { filePath = _filePath, error = moveEx.Message },
                            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                        });
                        File.AppendAllText(AgentDebugLogPath, line + Environment.NewLine);
                    }
                    catch
                    {
                        // no-op
                    }
                    #endregion

                    // Last-resort fallback for restrictive file-share scenarios.
                    await File.WriteAllTextAsync(_filePath, json, cancellationToken).ConfigureAwait(false);
                    if (File.Exists(temp))
                    {
                        File.Delete(temp);
                    }
                    return;
                }
            }
            else
            {
                File.Move(temp, _filePath);
            }
        }
        finally
        {
            if (File.Exists(temp))
            {
                File.Delete(temp);
            }
        }

        _logger.LogDebug("Preferences saved to {Path}", _filePath);
    }

    /// <inheritdoc />
    public void Dispose() => _lock.Dispose();
}
