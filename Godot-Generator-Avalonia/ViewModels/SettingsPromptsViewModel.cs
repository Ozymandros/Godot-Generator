using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Godot_Generator_Avalonia.Models;
using Godot_Generator_Avalonia.Services;
using GodotGenerator.Application;
using GodotGenerator.Application.Configuration;
using GodotGenerator.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Godot_Generator_Avalonia.ViewModels;

/// <summary>System prompts grid backed by <see cref="PreferenceKeys.PromptsSystemV1"/>.</summary>
public partial class SettingsPromptsViewModel : ViewModelBase
{
    private readonly IServiceScopeFactory _scopeFactory;

    public SettingsPromptsViewModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        foreach (var key in PromptRowsOrder)
        {
            PromptRows.Add(new PromptRowViewModel(key, Humanize(key)));
        }
    }

    private static readonly string[] PromptRowsOrder =
    [
        "scenes", "text", "code", "image", "audio", "music", "video", "sprites", "godot-ui", "godot-physics", "godot-project",
    ];

    public ObservableCollection<PromptRowViewModel> PromptRows { get; } = new();

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _errorMessage;

    public string InfoMessage =>
        "Prompts are stored in preferences (prompts.system.v1). They are merged with generation requests. " +
        "Legacy keys (prompts.text, etc.) are read until migrated.";

    public void ApplySnapshot(SettingsSnapshot snapshot)
    {
        ErrorMessage = null;
        foreach (var row in PromptRows)
        {
            row.Text = snapshot.SystemPrompts.GetValueOrDefault(row.ModalityKey) ?? string.Empty;
        }
    }

    public void SetError(string message) => ErrorMessage = message;

    [RelayCommand]
    private async Task SaveAsync()
    {
        StatusMessage = null;
        ErrorMessage = null;
        var doc = new SystemPromptsDocument { Version = 1 };
        foreach (var row in PromptRows)
        {
            var t = row.Text.Trim();
            if (t.Length > 0)
            {
                doc.Prompts[row.ModalityKey] = t;
            }
        }

        var json = ConfigurationRegistryService.Serialize(doc);
        using var scope = _scopeFactory.CreateScope();
        var api = scope.ServiceProvider.GetRequiredService<IGeneratorApiClient>();
        var ok = await api.SetPreferenceAsync(PreferenceKeys.PromptsSystemV1, json).ConfigureAwait(true);
        StatusMessage = ok ? "Prompts saved." : "Save failed.";
        if (!ok)
        {
            ErrorMessage = "Failed to save prompts.";
        }
    }

    [RelayCommand]
    private void ResetToDefaults()
    {
        var defaults = SystemPromptDefaults.CreateDocument();
        foreach (var row in PromptRows)
        {
            row.Text = defaults.Prompts.GetValueOrDefault(row.ModalityKey) ?? string.Empty;
        }

        StatusMessage = "Reset to defaults in the editor. Save to persist.";
        ErrorMessage = null;
    }

    private static string Humanize(string key) => key switch
    {
        "godot-ui" => "Godot UI",
        "godot-physics" => "Godot Physics",
        "godot-project" => "Godot Project",
        _ => key.Length == 0 ? key : char.ToUpperInvariant(key[0]) + key[1..],
    };
}

/// <summary>Single modality prompt row.</summary>
public partial class PromptRowViewModel : ObservableObject
{
    public PromptRowViewModel(string modalityKey, string label)
    {
        ModalityKey = modalityKey;
        Label = label;
    }

    public string ModalityKey { get; }

    public string Label { get; }

    [ObservableProperty]
    private string _text = string.Empty;
}
