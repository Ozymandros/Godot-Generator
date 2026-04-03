using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
        NavItems = new List<NavItem>
        {
            new NavItem("CogOutline", "Config"),
            new NavItem("ViewDashboardOutline", "Scenes"),
            new NavItem("Xml", "Code"),
            new NavItem("FileDocumentOutline", "Text"),
            new NavItem("ImageOutline", "Image"),
            new NavItem("Grid", "Sprites"),
            new NavItem("VolumeHigh", "Audio"),
            new NavItem("MovieOutline", "Video"),
            new NavItem("PaletteOutline", "Godot UI"),
            new NavItem("Atom", "Godot Physics"),
            new NavItem("Unity", "Godot Project"),
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
        1 => _panels[GenerationModality.Scenes],
        2 => _panels[GenerationModality.Code],
        3 => _panels[GenerationModality.Text],
        4 => _panels[GenerationModality.Image],
        5 => _panels[GenerationModality.Sprites],
        6 => _panels[GenerationModality.Audio],
        7 => _panels[GenerationModality.Video],
        8 => _panels[GenerationModality.GodotUi],
        9 => _panels[GenerationModality.GodotPhysics],
        10 => _panels[GenerationModality.GodotProject],
        _ => Config,
    };

    partial void OnSelectedNavIndexChanged(int value)
    {
        OnPropertyChanged(nameof(CurrentPane));
    }

    [RelayCommand]
    private void OpenRepo()
    {
        try
        {
            var url = "https://github.com/Ozymandros/Godot-Generator-Avalonia";
            if (OperatingSystem.IsWindows())
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            else if (OperatingSystem.IsLinux())
            {
                System.Diagnostics.Process.Start("xdg-open", url);
            }
            else if (OperatingSystem.IsMacOS())
            {
                System.Diagnostics.Process.Start("open", url);
            }
        }
        catch
        {
            // best effort
        }
    }
}
