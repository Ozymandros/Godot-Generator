using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Godot_Generator_Avalonia.Models;
using Godot_Generator_Avalonia.Services;
using GodotGenerator.Application;
using Microsoft.Extensions.DependencyInjection;

namespace Godot_Generator_Avalonia.ViewModels;

/// <summary>
/// General settings: default generation language and host snapshot summary.
/// </summary>
public partial class SettingsGeneralViewModel : ViewModelBase
{
    private readonly IServiceScopeFactory _scopeFactory;

    public SettingsGeneralViewModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        LanguageOptions = LanguageCatalog.Supported.ToArray();
        _selectedLanguage = "csharp";
    }

    public IReadOnlyList<string> LanguageOptions { get; }

    [ObservableProperty]
    private string _selectedLanguage;

    [ObservableProperty]
    private string _defaultLlmProvider = string.Empty;

    [ObservableProperty]
    private string _defaultChatModelId = string.Empty;

    [ObservableProperty]
    private int _godotToolCount;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>Applies aggregated snapshot from the host.</summary>
    public void ApplySnapshot(SettingsSnapshot snap)
    {
        ErrorMessage = null;
        DefaultLlmProvider = snap.DefaultLlmProvider;
        DefaultChatModelId = snap.DefaultChatModelId;
        GodotToolCount = snap.GodotToolNames.Count;

        if (snap.Preferences.TryGetValue(PreferenceKeys.PreferredLanguage, out var lang) &&
            !string.IsNullOrWhiteSpace(lang))
        {
            var match = LanguageOptions.FirstOrDefault(x => string.Equals(x, lang, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                SelectedLanguage = match;
            }
        }
    }

    public void SetError(string message) => ErrorMessage = message;

    [RelayCommand]
    private async Task SaveAsync()
    {
        StatusMessage = null;
        ErrorMessage = null;
        using var scope = _scopeFactory.CreateScope();
        var api = scope.ServiceProvider.GetRequiredService<IGeneratorApiClient>();
        var ok = await api.SaveGlobalPreferredLanguageAsync(SelectedLanguage).ConfigureAwait(true);
        StatusMessage = ok ? "Saved." : "Save failed.";
    }
}
