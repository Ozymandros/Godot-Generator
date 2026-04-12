using GodotGenerator.Blazor.Client.Models;
using Xunit;

namespace GodotGenerator.Blazor.Tests;

/// <summary>
/// Tests for URL slug mapping used by <c>/generate/{slug}</c> routes.
/// </summary>
public sealed class ModalityRoutesTests
{
    public static TheoryData<string, GenerationModality> Slugs =>
        new()
        {
            { "text", GenerationModality.Text },
            { "TEXT", GenerationModality.Text },
            { "code", GenerationModality.Code },
            { "animations", GenerationModality.Animations },
            { "godot-ui", GenerationModality.GodotUi },
            { "godot-project", GenerationModality.GodotProject },
            { "scenes", GenerationModality.Scenes },
        };

    [Theory]
    [MemberData(nameof(Slugs))]
    public void TryGet_recognizes_routes(string slug, GenerationModality expected)
    {
        Assert.True(ModalityRoutes.TryGet(slug, out var m));
        Assert.Equal(expected, m);
    }

    [Fact]
    public void TryGet_returns_false_for_unknown_slug()
    {
        Assert.False(ModalityRoutes.TryGet("unknown-panel", out _));
    }

    [Fact]
    public void GetSlug_roundtrips_TryGet_for_each_modality()
    {
        foreach (GenerationModality modality in Enum.GetValues<GenerationModality>())
        {
            var slug = ModalityRoutes.GetSlug(modality);
            Assert.True(ModalityRoutes.TryGet(slug, out var roundTrip));
            Assert.Equal(modality, roundTrip);
        }
    }
}
