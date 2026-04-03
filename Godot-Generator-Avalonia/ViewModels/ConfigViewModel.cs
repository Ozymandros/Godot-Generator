using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Godot_Generator_Avalonia.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Godot_Generator_Avalonia.ViewModels;

/// <summary>
/// Settings shell: General, Providers, Models, Prompts, Secrets.
/// </summary>
public partial class ConfigViewModel : ViewModelBase
{
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>
    /// Creates the settings shell and loads host configuration.
    /// </summary>
    public ConfigViewModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        General = new SettingsGeneralViewModel(scopeFactory);
        Providers = new SettingsProvidersViewModel(scopeFactory);
        Models = new SettingsModelsViewModel(scopeFactory);
        Prompts = new SettingsPromptsViewModel();
        Secrets = new SettingsSecretsViewModel(scopeFactory);
        _ = ReloadAllAsync();
    }

    /// <summary>General tab.</summary>
    public SettingsGeneralViewModel General { get; }

    /// <summary>Providers tab.</summary>
    public SettingsProvidersViewModel Providers { get; }

    /// <summary>Models tab.</summary>
    public SettingsModelsViewModel Models { get; }

    /// <summary>Prompts tab.</summary>
    public SettingsPromptsViewModel Prompts { get; }

    /// <summary>Secrets tab.</summary>
    public SettingsSecretsViewModel Secrets { get; }

    private int _selectedSettingsTab;

    public int SelectedSettingsTab
    {
        get => _selectedSettingsTab;
        set => SetProperty(ref _selectedSettingsTab, value);
    }

    private bool _isReloading;

    public bool IsReloading
    {
        get => _isReloading;
        private set => SetProperty(ref _isReloading, value);
    }

    private string? _shellErrorMessage;

    public string? ShellErrorMessage
    {
        get => _shellErrorMessage;
        private set => SetProperty(ref _shellErrorMessage, value);
    }

    /// <summary>Reloads all sections from the host.</summary>
    [RelayCommand]
    private async Task ReloadAllAsync()
    {
        ShellErrorMessage = null;
        IsReloading = true;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var api = scope.ServiceProvider.GetRequiredService<IGeneratorApiClient>();
            var snap = await api.GetAllConfigSnapshotAsync().ConfigureAwait(true);
            if (snap is null)
            {
                const string err = "Failed to load configuration from the host.";
                ShellErrorMessage = err;
                General.SetError(err);
                Providers.SetError(err);
                Models.SetError(err);
                Secrets.SetError(err);
                return;
            }

            General.ApplySnapshot(snap);
            Providers.ApplySnapshot(snap);
            Models.ApplySnapshot(snap);
            await Secrets.LoadFromSnapshotAsync(snap).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            // Avoid taking down the whole app during async startup.
            var err = $"Failed to load settings: {ex.Message}";
            ShellErrorMessage = err;
            General.SetError(err);
            Providers.SetError(err);
            Models.SetError(err);
            Secrets.SetError(err);
        }
        finally
        {
            IsReloading = false;
        }
    }
}
