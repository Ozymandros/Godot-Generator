#nullable enable

namespace GodotGenerator.Blazor.Client.Models;

/// <summary>
/// Maps each <see cref="GenerationModality"/> to registry modality tokens used in
/// <see cref="ProviderRegistryEntry.Modalities"/> and <see cref="ModelRegistryEntry.Modality"/>.
/// </summary>
public static class GenerationModalityRegistryTags
{
    /// <summary>Returns tags a panel accepts; provider/model rows match if they intersect this set.</summary>
    public static HashSet<string> GetAllowedRegistryTags(GenerationModality modality)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        switch (modality)
        {
            case GenerationModality.Text:
                set.Add("llm");
                set.Add("text");
                break;
            case GenerationModality.Code:
                set.Add("llm");
                set.Add("code");
                break;
            case GenerationModality.Image:
                set.Add("image");
                break;
            case GenerationModality.Audio:
                set.Add("audio");
                set.Add("music");
                break;
            case GenerationModality.Video:
                set.Add("video");
                break;
            case GenerationModality.Sprites:
                set.Add("sprites");
                break;
            case GenerationModality.Scenes:
                set.Add("scenes");
                break;
            case GenerationModality.Animations:
                set.Add("animations");
                break;
            case GenerationModality.GodotUi:
            case GenerationModality.GodotPhysics:
            case GenerationModality.GodotProject:
            case GenerationModality.GodotLighting:
            case GenerationModality.GodotCamera:
            case GenerationModality.GodotShaders:
            case GenerationModality.GodotSignals:
            case GenerationModality.GodotNodes:
                set.Add("llm");
                break;
        }

        return set;
    }
}
