using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Godot_Generator_Avalonia.Models;
using Godot_Generator_Avalonia.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Godot_Generator_Avalonia.ViewModels;

/// <summary>
/// One generation panel: prompt, optional language override, generate, output.
/// </summary>
public partial class GenerationPanelViewModel : ViewModelBase
{
    private readonly IServiceScopeFactory _scopeFactory;
    private CancellationTokenSource? _runCts;

    /// <summary>
    /// Creates a panel for the given modality.
    /// </summary>
    public GenerationPanelViewModel(IServiceScopeFactory scopeFactory, GenerationModality modality)
    {
        _scopeFactory = scopeFactory;
        Modality = modality;
        var langs = new List<string> { string.Empty };
        langs.AddRange(LanguageCatalog.Supported);
        LanguageOptions = langs;
    }

    /// <summary>Panel modality.</summary>
    public GenerationModality Modality { get; }

    /// <summary>Header text for the panel.</summary>
    public string DisplayTitle => Modality switch
    {
        GenerationModality.GodotUi => "Godot UI",
        GenerationModality.GodotPhysics => "Godot Physics",
        GenerationModality.Scenes => "Create Scene",
        GenerationModality.GodotProject => "Create Godot Project",
        _ => Modality.ToString(),
    };

    /// <summary>Label for the main prompt field.</summary>
    public string PromptLabel => Modality switch
    {
        GenerationModality.GodotPhysics => "Physics Description",
        GenerationModality.GodotUi => "UI Element Description",
        GenerationModality.Scenes => "Scene Description",
        GenerationModality.GodotProject => "Project Requirements",
        _ => "Prompt",
    };

    /// <summary>Watermark for the main prompt field.</summary>
    public string PromptWatermark => Modality switch
    {
        GenerationModality.GodotPhysics => "Enter physics simulation details...",
        GenerationModality.GodotUi => "Enter UI layout or component description...",
        GenerationModality.Scenes => "Describe the scene layout...",
        GenerationModality.GodotProject => "Enter your project needs...",
        _ => "Enter your prompt",
    };

    /// <summary>Combo items: empty string = use global default.</summary>
    public IReadOnlyList<string> LanguageOptions { get; }

    [ObservableProperty]
    private string _prompt = string.Empty;

    /// <summary>Per-panel language override (empty = use global).</summary>
    [ObservableProperty]
    private string _languageOverride = string.Empty;

    [ObservableProperty]
    private string? _responseText;

    [ObservableProperty]
    private string? _error;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GenerateCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string _effectiveProvider = string.Empty;

    [ObservableProperty]
    private string _effectiveModelId = string.Empty;

    /// <summary>Runs generation via the API client.</summary>
    [RelayCommand(CanExecute = nameof(CanGenerate))]
    private async Task GenerateAsync()
    {
        _runCts?.Dispose();
        _runCts = new CancellationTokenSource();
        IsBusy = true;
        Error = null;
        ResponseText = null;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var api = scope.ServiceProvider.GetRequiredService<IGeneratorApiClient>();
            var (provider, modelId) = await api
                .GetEffectiveProviderModelAsync(Modality, _runCts.Token)
                .ConfigureAwait(true);
            EffectiveProvider = provider ?? "(default)";
            EffectiveModelId = modelId ?? "(default)";
            var global = await api.GetGlobalPreferredLanguageAsync().ConfigureAwait(true);
            var (ok, message, err) = await api
                .GenerateAsync(Modality, Prompt, LanguageOverride, global, _runCts.Token)
                .ConfigureAwait(true);
            if (!ok)
            {
                Error = err ?? "Generation failed.";
                return;
            }

            ResponseText = message;
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel()
    {
        _runCts?.Cancel();
    }

    private bool CanGenerate() => !IsBusy;
    private bool CanCancel() => IsBusy;
}
