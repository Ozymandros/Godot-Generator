using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Godot_Generator_Avalonia.Models;
using Godot_Generator_Avalonia.Services;
using GodotGenerator.Application;
using Microsoft.Extensions.DependencyInjection;

namespace Godot_Generator_Avalonia.ViewModels;

/// <summary>
/// Preferred model ids per modality.
/// </summary>
public partial class SettingsModelsViewModel : ViewModelBase
{
    private readonly IServiceScopeFactory _scopeFactory;

    public SettingsModelsViewModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    [ObservableProperty]
    private string _llmModel = string.Empty;

    [ObservableProperty]
    private string _imageModel = string.Empty;

    [ObservableProperty]
    private string _audioModel = string.Empty;

    [ObservableProperty]
    private string _videoModel = string.Empty;

    [ObservableProperty]
    private string _hostDefaultChatModelId = string.Empty;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>Applies snapshot.</summary>
    public void ApplySnapshot(SettingsSnapshot snap)
    {
        ErrorMessage = null;
        HostDefaultChatModelId = snap.DefaultChatModelId;
        LlmModel = snap.Preferences.GetValueOrDefault(PreferenceKeys.PreferredLlmModel) ?? string.Empty;
        ImageModel = snap.Preferences.GetValueOrDefault(PreferenceKeys.PreferredImageModel) ?? string.Empty;
        AudioModel = snap.Preferences.GetValueOrDefault(PreferenceKeys.PreferredAudioModel) ?? string.Empty;
        VideoModel = snap.Preferences.GetValueOrDefault(PreferenceKeys.PreferredVideoModel) ?? string.Empty;
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
            (PreferenceKeys.PreferredLlmModel, NullIfEmpty(LlmModel)),
            (PreferenceKeys.PreferredImageModel, NullIfEmpty(ImageModel)),
            (PreferenceKeys.PreferredAudioModel, NullIfEmpty(AudioModel)),
            (PreferenceKeys.PreferredVideoModel, NullIfEmpty(VideoModel)),
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
