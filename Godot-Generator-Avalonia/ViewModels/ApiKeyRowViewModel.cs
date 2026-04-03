using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Godot_Generator_Avalonia.ViewModels;

/// <summary>
/// One API key row: service name, optional new secret, remove.
/// </summary>
public partial class ApiKeyRowViewModel : ViewModelBase
{
    private readonly Func<ApiKeyRowViewModel, Task> _onRemove;

    /// <summary>
    /// Creates a row for an API key slot.
    /// </summary>
    public ApiKeyRowViewModel(string serviceName, bool hasConfiguredKey, Func<ApiKeyRowViewModel, Task> onRemove)
    {
        _serviceName = serviceName;
        _hasConfiguredKey = hasConfiguredKey;
        _onRemove = onRemove;
    }

    [ObservableProperty]
    private string _serviceName;

    [ObservableProperty]
    private bool _hasConfiguredKey;

    [ObservableProperty]
    private string _newKeyValue = string.Empty;

    /// <summary>Removes stored key for this service (host API).</summary>
    [RelayCommand]
    private async Task RemoveAsync()
    {
        await _onRemove(this).ConfigureAwait(true);
    }
}
