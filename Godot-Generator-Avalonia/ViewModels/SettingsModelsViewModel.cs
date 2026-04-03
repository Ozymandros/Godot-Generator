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

/// <summary>Per-provider model registry with add/remove rows.</summary>
public partial class SettingsModelsViewModel : ViewModelBase
{
    private readonly IServiceScopeFactory _scopeFactory;

    public SettingsModelsViewModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public ObservableCollection<string> ProviderFilterOptions { get; } = new();

    [ObservableProperty]
    private string _selectedProviderFilter = string.Empty;

    public ObservableCollection<ModelRegistryEntry> Rows { get; } = new();

    [ObservableProperty]
    private string _newFriendlyName = string.Empty;

    [ObservableProperty]
    private string _newEngineValue = string.Empty;

    [ObservableProperty]
    private string _newModality = "llm";

    public IReadOnlyList<string> ModalityOptions { get; } = new[] { "llm", "image", "audio", "video" };

    [ObservableProperty]
    private string _hostDefaultChatModelId = string.Empty;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _errorMessage;

    public void ApplySnapshot(SettingsSnapshot snap)
    {
        ErrorMessage = null;
        HostDefaultChatModelId = snap.DefaultChatModelId;
        _allModels = snap.ModelsByProvider.Values.SelectMany(x => x).Select(CloneRow).ToList();
        ProviderFilterOptions.Clear();
        foreach (var id in snap.ProviderRegistry.Select(p => p.Id).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase))
        {
            ProviderFilterOptions.Add(id);
        }

        if (string.IsNullOrWhiteSpace(SelectedProviderFilter) || !ProviderFilterOptions.Contains(SelectedProviderFilter))
        {
            SelectedProviderFilter = ProviderFilterOptions.FirstOrDefault() ?? string.Empty;
        }

        RefreshRowsForFilter();
    }

    public void SetError(string message) => ErrorMessage = message;

    partial void OnSelectedProviderFilterChanged(string value) => RefreshRowsForFilter();

    private List<ModelRegistryEntry> _allModels = new();

    private void RefreshRowsForFilter()
    {
        Rows.Clear();
        var pid = SelectedProviderFilter.Trim();
        foreach (var m in _allModels.Where(x => string.Equals(x.ProviderId, pid, StringComparison.OrdinalIgnoreCase)))
        {
            Rows.Add(m);
        }
    }

    [RelayCommand]
    private async Task AddToProviderAsync()
    {
        StatusMessage = null;
        ErrorMessage = null;
        var pid = SelectedProviderFilter.Trim();
        if (string.IsNullOrEmpty(pid))
        {
            ErrorMessage = "Select a provider first.";
            return;
        }

        var friendly = NewFriendlyName.Trim();
        var engine = NewEngineValue.Trim();
        if (string.IsNullOrEmpty(friendly) || string.IsNullOrEmpty(engine))
        {
            ErrorMessage = "Friendly name and engine value are required.";
            return;
        }

        var row = new ModelRegistryEntry
        {
            ProviderId = pid,
            FriendlyName = friendly,
            EngineValue = engine,
            Modality = NewModality.Trim().ToLowerInvariant(),
        };

        _allModels.RemoveAll(x =>
            string.Equals(x.ProviderId, row.ProviderId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.EngineValue, row.EngineValue, StringComparison.OrdinalIgnoreCase));
        _allModels.Add(row);
        await PersistRegistryAsync().ConfigureAwait(true);
        NewFriendlyName = string.Empty;
        NewEngineValue = string.Empty;
        RefreshRowsForFilter();
        StatusMessage = "Model added.";
    }

    [RelayCommand]
    private async Task RemoveModelAsync(ModelRegistryEntry? row)
    {
        if (row is null)
        {
            return;
        }

        _allModels.RemoveAll(x =>
            string.Equals(x.ProviderId, row.ProviderId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.EngineValue, row.EngineValue, StringComparison.OrdinalIgnoreCase));
        await PersistRegistryAsync().ConfigureAwait(true);
        RefreshRowsForFilter();
        StatusMessage = "Model removed.";
    }

    private async Task PersistRegistryAsync()
    {
        var doc = new ModelRegistryDocument { Version = 1, Models = _allModels.Select(CloneRow).ToList() };
        var err = ConfigurationRegistryService.ValidateModelRegistry(doc);
        if (err is not null)
        {
            ErrorMessage = err;
            return;
        }

        var json = ConfigurationRegistryService.Serialize(doc);
        using var scope = _scopeFactory.CreateScope();
        var api = scope.ServiceProvider.GetRequiredService<IGeneratorApiClient>();
        var ok = await api.SetPreferenceAsync(PreferenceKeys.ModelsRegistryV1, json).ConfigureAwait(true);
        if (!ok)
        {
            ErrorMessage = "Failed to save model registry.";
        }
    }

    private static ModelRegistryEntry CloneRow(ModelRegistryEntry m) =>
        new()
        {
            ProviderId = m.ProviderId,
            FriendlyName = m.FriendlyName,
            EngineValue = m.EngineValue,
            Modality = m.Modality,
        };
}
