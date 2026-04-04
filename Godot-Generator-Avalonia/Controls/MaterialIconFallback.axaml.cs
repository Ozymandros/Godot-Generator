using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Godot_Generator_Avalonia.Controls;

public partial class MaterialIconFallback : UserControl
{
    public static readonly StyledProperty<string> IconKeyProperty = AvaloniaProperty.Register<MaterialIconFallback, string>(nameof(IconKey));

    public string IconKey
    {
        get => GetValue(IconKeyProperty);
        set => SetValue(IconKeyProperty, value);
    }

    public MaterialIconFallback()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IconKeyProperty)
        {
            UpdateIcon(change.NewValue as string);
        }
    }

    private void UpdateIcon(string? key)
    {
        var text = key switch
        {
            "CogOutline" or "settings" => "⚙",
            "FileDocumentOutline" or "text" => "📝",
            "Xml" or "code" => "</>",
            "ImageOutline" or "image" => "🖼",
            "VolumeHigh" or "audio" => "🔊",
            "MovieOutline" or "movie" or "video" => "🎬",
            "Grid" or "grid" or "sprites" => "▦",
            "PaletteOutline" or "godot_ui" => "🎨",
            "Atom" or "godot_physics" => "⚛",
            "ViewDashboardOutline" or "scenes" => "📊",
            "Unity" or "godot_project" => "📽",
            "GestureTapButton" or "animations" => "🕺",
            "RobotOutline" or "robot" or "providers" or "models" => "🤖",
            "ScriptTextOutline" or "prompts" => "📜",
            "KeyChain" or "secrets" => "🔑",
            "Github" or "github" or "repo" => "🐙",
            "CircleMedium" or "default" => "•",
            _ => "•",
        };

        if (this.FindControl<TextBlock>("IconText") is TextBlock tb)
        {
            tb.Text = text;
        }
    }
}
