using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Godot_Generator_Avalonia.Models;
using Microsoft.Extensions.DependencyInjection;
using Avalonia;
using Avalonia.Input;
using Avalonia.Controls.ApplicationLifetimes;

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
            new NavItem("GestureTapButton", "Animations"),
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

    [RelayCommand]
    private async Task NewProjectAsync()
    {
        StatusMessage = "New project created";
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task OpenProjectAsync()
    {
        StatusMessage = "Project opened";
        await Task.CompletedTask;
    }

    [RelayCommand]
    private void Undo() { }

    [RelayCommand]
    private void Redo() { }

    [RelayCommand]
    private void Copy()
    {
        var focused = GetFocusedControl() as Avalonia.Controls.TextBox;
        if (focused != null && focused.SelectionStart < focused.SelectionEnd)
        {
            var selectedText = focused.Text?.Substring(focused.SelectionStart, focused.SelectionEnd - focused.SelectionStart) ?? "";
            SetClipboard(selectedText);
            StatusMessage = $"Copied {selectedText.Length} characters";
        }
        else
        {
            StatusMessage = "No text selected. Focus: " + (focused?.Name ?? "No TextBox focused");
        }
    }

    [RelayCommand]
    private void Cut()
    {
        var focused = GetFocusedControl() as Avalonia.Controls.TextBox;
        if (focused != null)
        {
            if (focused.SelectionStart < focused.SelectionEnd)
            {
                var selectedText = focused.Text?.Substring(focused.SelectionStart, focused.SelectionEnd - focused.SelectionStart) ?? "";
                SetClipboard(selectedText);
                focused.Text = focused.Text?.Remove(focused.SelectionStart, focused.SelectionEnd - focused.SelectionStart);
                focused.CaretIndex = focused.SelectionStart;
                StatusMessage = $"Cut {selectedText.Length} characters";
            }
            else
            {
                StatusMessage = "No text selected to cut";
            }
        }
        else
        {
            StatusMessage = "No TextBox focused";
        }
    }

    [RelayCommand]
    private void Paste()
    {
        var focused = GetFocusedControl() as Avalonia.Controls.TextBox;
        if (focused != null)
        {
            var clipboard = GetClipboard();
            if (!string.IsNullOrEmpty(clipboard))
            {
                if (focused.SelectionStart < focused.SelectionEnd)
                {
                    focused.Text = focused.Text?.Remove(focused.SelectionStart, focused.SelectionEnd - focused.SelectionStart);
                }
                focused.Text = focused.Text?.Insert(focused.CaretIndex, clipboard);
                focused.CaretIndex += clipboard.Length;
                StatusMessage = $"Pasted {clipboard.Length} characters";
            }
            else
            {
                StatusMessage = "Clipboard is empty";
            }
        }
        else
        {
            StatusMessage = "No TextBox focused";
        }
    }

    [RelayCommand]
    private void SelectAll()
    {
        var focused = GetFocusedControl() as Avalonia.Controls.TextBox;
        if (focused != null && !string.IsNullOrEmpty(focused.Text))
        {
            focused.SelectAll();
            StatusMessage = $"Selected {focused.Text.Length} characters";
        }
        else
        {
            StatusMessage = "No TextBox to select or TextBox is empty";
        }
    }

    private Avalonia.Input.IInputElement? GetFocusedControl()
    {
        // Prefer last-known focused element which we maintain from the view because
        // opening the Menu steals logical focus on some platforms. Fall back to
        // querying the window focus manager if we don't have a cached value.
        if (_lastFocused is not null)
            return _lastFocused;

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime && lifetime.MainWindow is Avalonia.Controls.Window w)
        {
            return w.FocusManager?.GetFocusedElement();
        }
        return null;
    }

    // Backing field updated by the view when focus changes.
    private Avalonia.Input.IInputElement? _lastFocused;

    // Called by the view (MainWindow) to inform us of focus changes.
    public void UpdateLastFocused(Avalonia.Input.IInputElement element)
    {
        _lastFocused = element;
    }

    // Expose the cached focused element for the view to read when needed.
    public Avalonia.Input.IInputElement? GetCachedFocusedElement() => _lastFocused;

    // Cached text and selection info captured when a TextBox had focus.
    private string? _cachedText;
    private int _cachedSelectionStart;
    private int _cachedSelectionLength;
    private int _cachedCaretIndex;

    // If the focused control belonged to a generation panel, cache its viewmodel
    // so we can operate on the bound text even if the UI selection is lost.
    private GenerationPanelViewModel? _cachedPanelViewModel;

    public void UpdateLastFocusedWithSelection(Avalonia.Input.IInputElement element, string? text, int selectionStart, int selectionLength, int caretIndex, GenerationPanelViewModel? panel)
    {
        _lastFocused = element;
        _cachedText = text ?? "";
        _cachedSelectionStart = selectionStart;
        _cachedSelectionLength = selectionLength;
        _cachedCaretIndex = caretIndex;
        _cachedPanelViewModel = panel;
    }

    public (GenerationPanelViewModel? panel, string text, int selStart, int selLen, int caret) GetCachedSelectionInfo()
    {
        return (_cachedPanelViewModel, _cachedText ?? "", _cachedSelectionStart, _cachedSelectionLength, _cachedCaretIndex);
    }

    private void SetClipboard(string text)
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime && lifetime.MainWindow is Avalonia.Controls.Window w)
        {
            if (Avalonia.Controls.TopLevel.GetTopLevel(w) is { Clipboard: { } clipboard })
            {
#pragma warning disable CS0618
                clipboard.SetTextAsync(text).GetAwaiter().GetResult();
#pragma warning restore CS0618
            }
        }
    }

    private string GetClipboard()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime && lifetime.MainWindow is Avalonia.Controls.Window w)
        {
            if (Avalonia.Controls.TopLevel.GetTopLevel(w) is { Clipboard: { } clipboard })
            {
#pragma warning disable CS0618
                return clipboard.GetTextAsync().GetAwaiter().GetResult() ?? "";
#pragma warning restore CS0618
            }
        }
        return "";
    }

    [RelayCommand]
    private async Task About()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime && lifetime.MainWindow is Avalonia.Controls.Window w)
        {
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
            var dialog = new Avalonia.Controls.Window
            {
                Title = "About Godot Generator",
                Width = 360,
                Height = 200,
                CanResize = false,
                WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner,
                Content = new Avalonia.Controls.StackPanel
                {
                    Margin = new Avalonia.Thickness(24),
                    Spacing = 12,
                    Children =
                    {
                        new Avalonia.Controls.TextBlock { Text = "Godot Generator", FontSize = 20, FontWeight = Avalonia.Media.FontWeight.Bold, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center },
                        new Avalonia.Controls.TextBlock { Text = $"Version: {version}", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center },
                        new Avalonia.Controls.TextBlock { Text = "AI-powered Godot project generator\nwith multi-modal content creation.", TextAlignment = Avalonia.Media.TextAlignment.Center, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center },
                    }
                }
            };
            await dialog.ShowDialog(w);
        }
    }

    [RelayCommand]
    private void Minimize()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime && lifetime.MainWindow is Avalonia.Controls.Window w)
        {
            w.WindowState = Avalonia.Controls.WindowState.Minimized;
        }
    }

    [RelayCommand]
    private void CloseWindow()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime && lifetime.MainWindow is Avalonia.Controls.Window w)
        {
            w.Close();
        }
    }

    [RelayCommand]
    private void ToggleFullscreen()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime && lifetime.MainWindow is Avalonia.Controls.Window w)
        {
            w.WindowState = w.WindowState == Avalonia.Controls.WindowState.FullScreen ? Avalonia.Controls.WindowState.Normal : Avalonia.Controls.WindowState.FullScreen;
        }
    }

    [ObservableProperty]
    private string _statusMessage = "Ready";
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
        11 => _panels[GenerationModality.Animations],
        _ => Config,
    };

    partial void OnSelectedNavIndexChanged(int value)
    {
        OnPropertyChanged(nameof(CurrentPane));
    }

    private void ShowNavigation(int index) => SelectedNavIndex = index;

    /// <summary>
    /// Selects a navigation section from menu commands.
    /// </summary>
    [RelayCommand]
    private void SelectNavigation(int index)
    {
        if (index >= 0 && index < NavItems.Count)
        {
            ShowNavigation(index);
        }
    }

    [RelayCommand] private void ShowConfig() => ShowNavigation(0);
    [RelayCommand] private void ShowScenes() => ShowNavigation(1);
    [RelayCommand] private void ShowCode() => ShowNavigation(2);
    [RelayCommand] private void ShowText() => ShowNavigation(3);
    [RelayCommand] private void ShowImage() => ShowNavigation(4);
    [RelayCommand] private void ShowSprites() => ShowNavigation(5);
    [RelayCommand] private void ShowAnimations() => ShowNavigation(6);
    [RelayCommand] private void ShowAudio() => ShowNavigation(7);
    [RelayCommand] private void ShowVideo() => ShowNavigation(8);
    [RelayCommand] private void ShowGodotUi() => ShowNavigation(9);
    [RelayCommand] private void ShowGodotPhysics() => ShowNavigation(10);
    [RelayCommand] private void ShowGodotProject() => ShowNavigation(11);

    /// <summary>
    /// Reloads configuration in the settings shell.
    /// </summary>
    [RelayCommand]
    private void ReloadConfiguration()
    {
        Config.ReloadAllCommand.Execute(null);
    }

    /// <summary>
    /// Opens the project repository in the default system browser.
    /// </summary>
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

    /// <summary>
    /// Opens the project documentation in the default system browser.
    /// </summary>
    [RelayCommand]
    private void OpenDocumentation()
    {
        try
        {
            var url = "https://github.com/Ozymandros/Godot-Generator-Avalonia/blob/master/README.md";
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

    /// <summary>
    /// Closes the desktop application.
    /// </summary>
    [RelayCommand]
    private void ExitApplication()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
        {
            lifetime.Shutdown();
        }
    }
}
