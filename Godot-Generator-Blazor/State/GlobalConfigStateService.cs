using Godot_Generator_Blazor.Services;
using Microsoft.Extensions.Logging;

namespace Godot_Generator_Blazor.State;

/// <summary>
/// Holds global configuration UI state and persistence operations.
/// </summary>
public sealed class GlobalConfigStateService(
    IApiClient apiClient,
    ILogger<GlobalConfigStateService> logger)
{
    /// <summary>
    /// Gets the current preferred language default.
    /// </summary>
    public string PreferredLanguage { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the latest load/save error.
    /// </summary>
    public string? Error { get; private set; }

    /// <summary>
    /// Gets whether a config operation is running.
    /// </summary>
    public bool IsBusy { get; private set; }

    /// <summary>
    /// Loads global config state from API preferences.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        Error = null;
        try
        {
            PreferredLanguage = await apiClient.GetGlobalPreferredLanguageAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load global config.");
            Error = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Updates in-memory preferred language selection.
    /// </summary>
    /// <param name="language">Selected language.</param>
    public void SetPreferredLanguage(string language)
    {
        PreferredLanguage = language ?? string.Empty;
    }

    /// <summary>
    /// Persists global preferred language to API preferences.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True when save succeeded.</returns>
    public async Task<bool> SaveAsync(CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        Error = null;
        try
        {
            var ok = await apiClient.SaveGlobalPreferredLanguageAsync(PreferredLanguage, cancellationToken).ConfigureAwait(false);
            if (!ok)
            {
                Error = "Failed to save preferred language.";
            }

            return ok;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save global config.");
            Error = ex.Message;
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
