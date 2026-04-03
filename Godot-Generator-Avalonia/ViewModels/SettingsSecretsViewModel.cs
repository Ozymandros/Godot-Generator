using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Godot_Generator_Avalonia.Models;
using Godot_Generator_Avalonia.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Godot_Generator_Avalonia.ViewModels;

/// <summary>API keys: table-style rows and modal for new secrets.</summary>
public partial class SettingsSecretsViewModel : ViewModelBase
{
    private readonly IServiceScopeFactory _scopeFactory;

    public SettingsSecretsViewModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public ObservableCollection<ApiKeyRowViewModel> Rows { get; } = new();

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isStoreDialogOpen;

    [ObservableProperty]
    private string _dialogServiceId = string.Empty;

    [ObservableProperty]
    private string _dialogKeyValue = string.Empty;

    public async Task LoadFromSnapshotAsync(SettingsSnapshot snap, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        ErrorMessage = null;
        StatusMessage = null;
        Rows.Clear();

        foreach (var name in snap.KeyNames)
        {
            Rows.Add(CreateRow(name, hasConfigured: true));
        }
    }

    public void SetError(string message)
    {
        ErrorMessage = message;
        Rows.Clear();
    }

    private ApiKeyRowViewModel CreateRow(string serviceName, bool hasConfigured) =>
        new(serviceName, hasConfigured, OnRemoveAsync);

    private async Task OnRemoveAsync(ApiKeyRowViewModel row)
    {
        using var scope = _scopeFactory.CreateScope();
        var api = scope.ServiceProvider.GetRequiredService<IGeneratorApiClient>();
        var ok = await api
            .SaveApiKeysAsync(new Dictionary<string, string?> { [row.ServiceName] = null }, CancellationToken.None)
            .ConfigureAwait(true);

        StatusMessage = ok ? $"Removed key for {row.ServiceName}." : "Remove failed.";
        if (ok)
        {
            Rows.Remove(row);
        }
    }

    [RelayCommand]
    private void OpenStoreDialog()
    {
        DialogServiceId = string.Empty;
        DialogKeyValue = string.Empty;
        IsStoreDialogOpen = true;
    }

    [RelayCommand]
    private void CancelStoreDialog()
    {
        IsStoreDialogOpen = false;
    }

    [RelayCommand]
    private async Task StoreFromDialogAsync()
    {
        var name = DialogServiceId.Trim();
        var key = DialogKeyValue.Trim();
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(key))
        {
            StatusMessage = "Service ID and key value are required.";
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var api = scope.ServiceProvider.GetRequiredService<IGeneratorApiClient>();
        var ok = await api.SaveApiKeysAsync(new Dictionary<string, string?> { [name] = key }).ConfigureAwait(true);
        if (!ok)
        {
            StatusMessage = "Store failed.";
            return;
        }

        var existing = Rows.FirstOrDefault(r => string.Equals(r.ServiceName, name, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            existing.HasConfiguredKey = true;
            existing.NewKeyValue = string.Empty;
        }
        else
        {
            Rows.Add(CreateRow(name, hasConfigured: true));
        }

        IsStoreDialogOpen = false;
        DialogServiceId = string.Empty;
        DialogKeyValue = string.Empty;
        StatusMessage = "Secret stored.";
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        StatusMessage = null;
        ErrorMessage = null;
        var toSave = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in Rows)
        {
            var trimmed = row.NewKeyValue.Trim();
            if (trimmed.Length > 0)
            {
                toSave[row.ServiceName] = trimmed;
            }
        }

        if (toSave.Count == 0)
        {
            StatusMessage = "Enter a new secret on a row, or use Store new secret.";
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var api = scope.ServiceProvider.GetRequiredService<IGeneratorApiClient>();
        var ok = await api.SaveApiKeysAsync(toSave).ConfigureAwait(true);
        if (!ok)
        {
            StatusMessage = "Save failed.";
            return;
        }

        foreach (var row in Rows)
        {
            if (toSave.ContainsKey(row.ServiceName))
            {
                row.HasConfiguredKey = true;
                row.NewKeyValue = string.Empty;
            }
        }

        StatusMessage = "Saved.";
    }
}
