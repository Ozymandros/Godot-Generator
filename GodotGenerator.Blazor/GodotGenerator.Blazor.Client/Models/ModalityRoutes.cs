namespace GodotGenerator.Blazor.Client.Models;

/// <summary>
/// Maps URL segments to <see cref="GenerationModality"/>.
/// </summary>
public static class ModalityRoutes
{
    private static readonly Dictionary<string, GenerationModality> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["text"] = GenerationModality.Text,
        ["code"] = GenerationModality.Code,
        ["image"] = GenerationModality.Image,
        ["audio"] = GenerationModality.Audio,
        ["video"] = GenerationModality.Video,
        ["sprites"] = GenerationModality.Sprites,
        ["godot-ui"] = GenerationModality.GodotUi,
        ["godot-physics"] = GenerationModality.GodotPhysics,
        ["scenes"] = GenerationModality.Scenes,
        ["godot-project"] = GenerationModality.GodotProject,
        ["animations"] = GenerationModality.Animations,
        ["godot-lighting"] = GenerationModality.GodotLighting,
        ["godot-camera"] = GenerationModality.GodotCamera,
        ["godot-shaders"] = GenerationModality.GodotShaders,
        ["godot-signals"] = GenerationModality.GodotSignals,
        ["godot-nodes"] = GenerationModality.GodotNodes,
    };

    public static bool TryGet(string slug, out GenerationModality modality) => Map.TryGetValue(slug, out modality);

    public static string GetSlug(GenerationModality modality) =>
        Map.FirstOrDefault(x => x.Value == modality).Key ?? "text";
}
