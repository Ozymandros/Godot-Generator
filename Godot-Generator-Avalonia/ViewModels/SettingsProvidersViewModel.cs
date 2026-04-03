using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Godot_Generator_Avalonia.Models;
using Godot_Generator_Avalonia.Services;
using GodotGenerator.Application;
using GodotGenerator.Application.Configuration;
using GodotGenerator.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Godot_Generator_Avalonia.ViewModels;

/// <summary>Provider registry: list + detail form with commit and deregister.</summary>
public partial class SettingsProvidersViewModel : ViewModelBase
{
    private readonly IServiceScopeFactory _scopeFactory;

    public SettingsProvidersViewModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public ObservableCollection<ProviderRegistryEntry> Entries { get; } = new();

    [ObservableProperty]
    private ProviderRegistryEntry? _selectedEntry;

    [ObservableProperty]
    private string _modalitiesString = string.Empty;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _errorMessage;

    public void ApplySnapshot(SettingsSnapshot snap)
    {
        ErrorMessage = null;
        Entries.Clear();
        foreach (var p in snap.ProviderRegistry)
        {
            Entries.Add(CloneEntry(p));
        }

        SelectedEntry = Entries.FirstOrDefault();
    }

    partial void OnSelectedEntryChanged(ProviderRegistryEntry? value)
    {
        ModalitiesString = value is null ? string.Empty : string.Join(", ", value.Modalities);
    }

    partial void OnModalitiesStringChanged(string value)
    {
        if (SelectedEntry is null)
        {
            return;
        }

        SelectedEntry.Modalities = value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    public void SetError(string message) => ErrorMessage = message;

    [RelayCommand]
    private void AddProvider()
    {
        var entry = new ProviderRegistryEntry
        {
            Id = "new-provider",
            KeyStoreHandle = "new-provider",
            Modalities = new List<string> { "llm" },
            AuthenticationRequired = true,
            Streaming = true,
            FunctionCalling = true,
            GenericToolUse = true,
        };
        Entries.Add(entry);
        SelectedEntry = entry;
    }

    [RelayCommand]
    private void Deregister()
    {
        if (SelectedEntry is null)
        {
            return;
        }

        Entries.Remove(SelectedEntry);
        SelectedEntry = Entries.FirstOrDefault();
    }

    [RelayCommand]
    private async Task CommitAsync()
    {
        StatusMessage = null;
        ErrorMessage = null;
        var doc = new ProviderRegistryDocument { Version = 1, Providers = Entries.ToList() };
        var err = ConfigurationRegistryService.ValidateProviderRegistry(doc);
        if (err is not null)
        {
            ErrorMessage = err;
            return;
        }

        var json = ConfigurationRegistryService.Serialize(doc);
        using var scope = _scopeFactory.CreateScope();
        var api = scope.ServiceProvider.GetRequiredService<IGeneratorApiClient>();
        var ok = await api.SetPreferenceAsync(PreferenceKeys.ProvidersRegistryV1, json).ConfigureAwait(true);
        StatusMessage = ok ? "Providers saved." : "Save failed.";
        if (!ok)
        {
            ErrorMessage = "Failed to persist provider registry.";
        }
    }

    private static ProviderRegistryEntry CloneEntry(ProviderRegistryEntry p) =>
        new()
        {
            Id = p.Id,
            KeyStoreHandle = p.KeyStoreHandle,
            Endpoint = p.Endpoint,
            OpenAiCompatibility = p.OpenAiCompatibility,
            AuthenticationRequired = p.AuthenticationRequired,
            Vision = p.Vision,
            Streaming = p.Streaming,
            FunctionCalling = p.FunctionCalling,
            GenericToolUse = p.GenericToolUse,
            Modalities = new List<string>(p.Modalities),
        };
}
