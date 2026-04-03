using CommunityToolkit.Mvvm.ComponentModel;
using Godot_Generator_Avalonia.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Godot_Generator_Avalonia.ViewModels;

/// <summary>
/// Shell: left navigation and current pane (config or generation panel).
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    private readonly Dictionary<GenerationModality, GenerationPanelViewModel> _panels;

    /// <summary>
    /// Creates the main window model with config and one view model per modality.
    /// </summary>
    public MainWindowViewModel(IServiceScopeFactory scopeFactory)
    {
        // Use exact Material.Icons enum names where possible to ensure icons resolve
        NavItems = new List<NavItem>
        {
            new NavItem("Settings", "Config"),
            new NavItem("CommentText", "Text"),
            new NavItem("Code", "Code"),
            new NavItem("Image", "Image"),
            new NavItem("MusicNote", "Audio"),
            new NavItem("Movie", "Video"),
            new NavItem("GridOn", "Sprites"),
            new NavItem("Widgets", "Godot UI"),
            new NavItem("Build", "Godot Physics"),
        };

        Config = new ConfigViewModel(scopeFactory);
        _panels = new Dictionary<GenerationModality, GenerationPanelViewModel>();
        foreach (GenerationModality m in Enum.GetValues<GenerationModality>())
        {
            _panels[m] = new GenerationPanelViewModel(scopeFactory, m);
        }
    }

    /// <summary>Left menu items (index matches <see cref="SelectedNavIndex"/>).</summary>
    public IReadOnlyList<NavItem> NavItems { get; }

    /// <summary>Global configuration pane.</summary>
    public ConfigViewModel Config { get; }

    [ObservableProperty]
    private int _selectedNavIndex;

    /// <summary>Current right-hand content: config or a generation panel.</summary>
    public object CurrentPane => SelectedNavIndex switch
    {
        0 => Config,
        1 => _panels[GenerationModality.Text],
        2 => _panels[GenerationModality.Code],
        3 => _panels[GenerationModality.Image],
        4 => _panels[GenerationModality.Audio],
        5 => _panels[GenerationModality.Video],
        6 => _panels[GenerationModality.Sprites],
        7 => _panels[GenerationModality.GodotUi],
        8 => _panels[GenerationModality.GodotPhysics],
        _ => Config,
    };

    partial void OnSelectedNavIndexChanged(int value)
    {
        OnPropertyChanged(nameof(CurrentPane));
    }
}
