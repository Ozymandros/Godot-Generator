#nullable enable
using GodotGenerator.Blazor.Client.Models;
using Xunit;

namespace GodotGenerator.Blazor.Tests;

public sealed class GenerationModalityRegistryTagsTests
{
    [Fact]
    public void Text_includes_llm_and_text_tags()
    {
        var tags = GenerationModalityRegistryTags.GetAllowedRegistryTags(GenerationModality.Text);
        Assert.Contains("llm", tags);
        Assert.Contains("text", tags);
    }

    [Fact]
    public void Code_includes_llm_and_code_tags()
    {
        var tags = GenerationModalityRegistryTags.GetAllowedRegistryTags(GenerationModality.Code);
        Assert.Contains("llm", tags);
        Assert.Contains("code", tags);
    }

    [Fact]
    public void GodotCamera_includes_llm_only()
    {
        var tags = GenerationModalityRegistryTags.GetAllowedRegistryTags(GenerationModality.GodotCamera);
        Assert.Contains("llm", tags);
        Assert.Single(tags);
    }
}
