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
            "settings" => "⚙",
            "text_fields" => "📝",
            "code" => "</>",
            "image" => "🖼",
            "music_note" => "🎵",
            "movie" => "🎬",
            "grid_on" => "▦",
            "widgets" => "▦",
            "build" => "🔧",
            _ => "•",
        };

        if (this.FindControl<TextBlock>("IconText") is TextBlock tb)
        {
            tb.Text = text;
        }
    }
}
