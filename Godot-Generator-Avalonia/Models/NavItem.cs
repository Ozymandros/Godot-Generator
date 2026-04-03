namespace Godot_Generator_Avalonia.Models;

public sealed class NavItem
{
    public NavItem(string iconKey, string label)
    {
        IconKey = iconKey;
        Label = label;
    }

    public string IconKey { get; }
    public string Label { get; }
}
