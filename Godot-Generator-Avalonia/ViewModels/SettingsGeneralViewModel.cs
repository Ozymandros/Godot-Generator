using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Godot_Generator_Avalonia.Models;
using Godot_Generator_Avalonia.Services;
using GodotGenerator.Application;
using GodotGenerator.Application.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Godot_Generator_Avalonia.ViewModels;

/// <summary>General settings: backend URL, output path, language, default provider/model per modality.</summary>
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
    private string _backendUrl = string.Empty;

    [ObservableProperty]
    private string _outputBasePath = string.Empty;

    [ObservableProperty]
    private string _selectedLanguage;

    [ObservableProperty]
    private string _defaultLlmProvider = string.Empty;

    [ObservableProperty]
    private string _defaultChatModelId = string.Empty;

    [ObservableProperty]
    private int _godotToolCount;

    [ObservableProperty]
    private string _llmProvider = string.Empty;

    [ObservableProperty]
    private string _llmModel = string.Empty;

    [ObservableProperty]
    private string _imageProvider = string.Empty;

    [ObservableProperty]
    private string _imageModel = string.Empty;

    [ObservableProperty]
    private string _audioProvider = string.Empty;

    [ObservableProperty]
    private string _audioModel = string.Empty;

    [ObservableProperty]
    private string _videoProvider = string.Empty;

    [ObservableProperty]
    private string _videoModel = string.Empty;

    public ObservableCollection<string> ProviderOptions { get; } = new();

    public ObservableCollection<string> LlmModelOptions { get; } = new();

    public ObservableCollection<string> ImageModelOptions { get; } = new();

    public ObservableCollection<string> AudioModelOptions { get; } = new();

    public ObservableCollection<string> VideoModelOptions { get; } = new();

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _errorMessage;

    private IReadOnlyDictionary<string, IReadOnlyList<ModelRegistryEntry>> _modelsByProvider =
        new Dictionary<string, IReadOnlyList<ModelRegistryEntry>>(StringComparer.OrdinalIgnoreCase);

    public void ApplySnapshot(SettingsSnapshot snap)
    {
        ErrorMessage = null;
        DefaultLlmProvider = snap.DefaultLlmProvider;
        DefaultChatModelId = snap.DefaultChatModelId;
        GodotToolCount = snap.GodotToolNames.Count;
        BackendUrl = snap.Preferences.GetValueOrDefault(PreferenceKeys.AppBackendUrl) ?? string.Empty;
        OutputBasePath = snap.Preferences.GetValueOrDefault(PreferenceKeys.AppOutputBasePath) ?? string.Empty;

        if (snap.Preferences.TryGetValue(PreferenceKeys.PreferredLanguage, out var lang) &&
            !string.IsNullOrWhiteSpace(lang))
        {
            var match = LanguageOptions.FirstOrDefault(x => string.Equals(x, lang, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                SelectedLanguage = match;
            }
        }

        _modelsByProvider = snap.ModelsByProvider;
        ProviderOptions.Clear();
        foreach (var p in snap.ProviderRegistry.Select(x => x.Id).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase))
        {
            ProviderOptions.Add(p);
        }

        LlmProvider = snap.Preferences.GetValueOrDefault(PreferenceKeys.PreferredLlmProvider) ?? string.Empty;
        ImageProvider = snap.Preferences.GetValueOrDefault(PreferenceKeys.PreferredImageProvider) ?? string.Empty;
        AudioProvider = snap.Preferences.GetValueOrDefault(PreferenceKeys.PreferredAudioProvider) ?? string.Empty;
        VideoProvider = snap.Preferences.GetValueOrDefault(PreferenceKeys.PreferredVideoProvider) ?? string.Empty;

        LlmModel = snap.Preferences.GetValueOrDefault(PreferenceKeys.PreferredLlmModel) ?? string.Empty;
        ImageModel = snap.Preferences.GetValueOrDefault(PreferenceKeys.PreferredImageModel) ?? string.Empty;
        AudioModel = snap.Preferences.GetValueOrDefault(PreferenceKeys.PreferredAudioModel) ?? string.Empty;
        VideoModel = snap.Preferences.GetValueOrDefault(PreferenceKeys.PreferredVideoModel) ?? string.Empty;

        RefreshModelOptions(LlmProvider, LlmModelOptions);
        RefreshModelOptions(ImageProvider, ImageModelOptions);
        RefreshModelOptions(AudioProvider, AudioModelOptions);
        RefreshModelOptions(VideoProvider, VideoModelOptions);
    }

    public void SetError(string message) => ErrorMessage = message;

    partial void OnLlmProviderChanged(string value) => RefreshModelOptions(value, LlmModelOptions);

    partial void OnImageProviderChanged(string value) => RefreshModelOptions(value, ImageModelOptions);

    partial void OnAudioProviderChanged(string value) => RefreshModelOptions(value, AudioModelOptions);

    partial void OnVideoProviderChanged(string value) => RefreshModelOptions(value, VideoModelOptions);

    private void RefreshModelOptions(string? providerId, ObservableCollection<string> target)
    {
        target.Clear();
        if (string.IsNullOrWhiteSpace(providerId))
        {
            return;
        }

        if (!_modelsByProvider.TryGetValue(providerId.Trim(), out var rows))
        {
            return;
        }

        foreach (var engine in rows.Select(r => r.EngineValue).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase))
        {
            target.Add(engine);
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        StatusMessage = null;
        ErrorMessage = null;
        using var scope = _scopeFactory.CreateScope();
        var api = scope.ServiceProvider.GetRequiredService<IGeneratorApiClient>();
        var pairs = new (string Key, string? Value)[]
        {
            (PreferenceKeys.AppBackendUrl, NullIfEmpty(BackendUrl)),
            (PreferenceKeys.AppOutputBasePath, NullIfEmpty(OutputBasePath)),
            (PreferenceKeys.PreferredLlmProvider, NullIfEmpty(LlmProvider)),
            (PreferenceKeys.PreferredLlmModel, NullIfEmpty(LlmModel)),
            (PreferenceKeys.PreferredImageProvider, NullIfEmpty(ImageProvider)),
            (PreferenceKeys.PreferredImageModel, NullIfEmpty(ImageModel)),
            (PreferenceKeys.PreferredAudioProvider, NullIfEmpty(AudioProvider)),
            (PreferenceKeys.PreferredAudioModel, NullIfEmpty(AudioModel)),
            (PreferenceKeys.PreferredVideoProvider, NullIfEmpty(VideoProvider)),
            (PreferenceKeys.PreferredVideoModel, NullIfEmpty(VideoModel)),
        };

        foreach (var (key, value) in pairs)
        {
            var ok = await api.SetPreferenceAsync(key, value).ConfigureAwait(true);
            if (!ok)
            {
                ErrorMessage = $"Save failed for {key}.";
                return;
            }
        }

        var langOk = await api.SaveGlobalPreferredLanguageAsync(SelectedLanguage).ConfigureAwait(true);
        if (!langOk)
        {
            ErrorMessage = "Failed to save preferred language.";
            return;
        }

        StatusMessage = "Saved.";
    }

    private static string? NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
