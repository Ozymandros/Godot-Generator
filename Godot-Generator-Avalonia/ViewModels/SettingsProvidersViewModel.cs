using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Godot_Generator_Avalonia.Models;
using Godot_Generator_Avalonia.Services;
using GodotGenerator.Application;
using Microsoft.Extensions.DependencyInjection;

namespace Godot_Generator_Avalonia.ViewModels;

/// <summary>
/// Preferred provider ids (OpenAI-compatible host strings).
/// </summary>
public partial class SettingsProvidersViewModel : ViewModelBase
{
    private readonly IServiceScopeFactory _scopeFactory;

    public SettingsProvidersViewModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    [ObservableProperty]
    private string _llmProvider = string.Empty;

    [ObservableProperty]
    private string _imageProvider = string.Empty;

    [ObservableProperty]
    private string _audioProvider = string.Empty;

    [ObservableProperty]
    private string _videoProvider = string.Empty;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>Applies snapshot preferences.</summary>
    public void ApplySnapshot(SettingsSnapshot snap)
    {
        ErrorMessage = null;
        LlmProvider = snap.Preferences.GetValueOrDefault(PreferenceKeys.PreferredLlmProvider) ?? string.Empty;
        ImageProvider = snap.Preferences.GetValueOrDefault(PreferenceKeys.PreferredImageProvider) ?? string.Empty;
        AudioProvider = snap.Preferences.GetValueOrDefault(PreferenceKeys.PreferredAudioProvider) ?? string.Empty;
        VideoProvider = snap.Preferences.GetValueOrDefault(PreferenceKeys.PreferredVideoProvider) ?? string.Empty;
    }

    public void SetError(string message) => ErrorMessage = message;

    [RelayCommand]
    private async Task SaveAsync()
    {
        StatusMessage = null;
        ErrorMessage = null;
        using var scope = _scopeFactory.CreateScope();
        var api = scope.ServiceProvider.GetRequiredService<IGeneratorApiClient>();
        var pairs = new (string Key, string? Value)[]
        {
            (PreferenceKeys.PreferredLlmProvider, NullIfEmpty(LlmProvider)),
            (PreferenceKeys.PreferredImageProvider, NullIfEmpty(ImageProvider)),
            (PreferenceKeys.PreferredAudioProvider, NullIfEmpty(AudioProvider)),
            (PreferenceKeys.PreferredVideoProvider, NullIfEmpty(VideoProvider)),
        };

        foreach (var (key, value) in pairs)
        {
            var ok = await api.SetPreferenceAsync(key, value).ConfigureAwait(true);
            if (!ok)
            {
                StatusMessage = $"Save failed for {key}.";
                return;
            }
        }

        StatusMessage = "Saved.";
    }

    private static string? NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
