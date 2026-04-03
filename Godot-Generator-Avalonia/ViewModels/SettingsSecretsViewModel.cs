using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Godot_Generator_Avalonia.Models;
using Godot_Generator_Avalonia.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Godot_Generator_Avalonia.ViewModels;

/// <summary>
/// API keys per service name (masked entry; never log secrets).
/// </summary>
public partial class SettingsSecretsViewModel : ViewModelBase
{
    private readonly IServiceScopeFactory _scopeFactory;

    public SettingsSecretsViewModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public ObservableCollection<ApiKeyRowViewModel> Rows { get; } = new();

    [ObservableProperty]
    private string _newServiceName = string.Empty;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>Rebuilds rows from snapshot and key presence (values are never shown).</summary>
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
            row.HasConfiguredKey = false;
            row.NewKeyValue = string.Empty;
        }
    }

    [RelayCommand]
    private void AddRow()
    {
        var name = NewServiceName.Trim();
        if (string.IsNullOrEmpty(name))
        {
            return;
        }

        if (Rows.Any(r => string.Equals(r.ServiceName, name, StringComparison.OrdinalIgnoreCase)))
        {
            StatusMessage = "That service is already listed.";
            return;
        }

        Rows.Add(CreateRow(name, hasConfigured: false));
        NewServiceName = string.Empty;
        StatusMessage = null;
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
            StatusMessage = "Enter a new secret for at least one service, or use Remove on a row.";
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
