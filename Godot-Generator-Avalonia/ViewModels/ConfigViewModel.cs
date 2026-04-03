using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Godot_Generator_Avalonia.Models;
using Godot_Generator_Avalonia.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Godot_Generator_Avalonia.ViewModels;

/// <summary>
/// Global configuration: default preferred language.
/// </summary>
public partial class ConfigViewModel : ViewModelBase
{
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>
    /// Creates the config view model and starts loading persisted values.
    /// </summary>
    public ConfigViewModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        LanguageOptions = LanguageCatalog.Supported.ToArray();
        _ = LoadAsync();
    }

    /// <summary>Combo box items.</summary>
    public IReadOnlyList<string> LanguageOptions { get; }

    [ObservableProperty]
    private string _selectedLanguage = "csharp";

    [ObservableProperty]
    private string? _statusMessage;

    /// <summary>Saves the global preferred language.</summary>
    [RelayCommand]
    private async Task SaveAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var api = scope.ServiceProvider.GetRequiredService<IGeneratorApiClient>();
        var ok = await api.SaveGlobalPreferredLanguageAsync(SelectedLanguage).ConfigureAwait(true);
        StatusMessage = ok ? "Saved." : "Save failed.";
    }

    private async Task LoadAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var api = scope.ServiceProvider.GetRequiredService<IGeneratorApiClient>();
        var lang = await api.GetGlobalPreferredLanguageAsync().ConfigureAwait(true);
        if (!string.IsNullOrWhiteSpace(lang))
        {
            var match = LanguageOptions.FirstOrDefault(x => string.Equals(x, lang, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                SelectedLanguage = match;
            }
        }
    }
}
