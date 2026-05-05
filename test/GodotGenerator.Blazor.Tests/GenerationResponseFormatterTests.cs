using GodotGenerator.Blazor.Client.Services;
using Xunit;

namespace GodotGenerator.Blazor.Tests;

/// <summary>
/// Regression tests for preview text extraction from generation payloads.
/// </summary>
public sealed class GenerationResponseFormatterTests
{
    [Fact]
    public void ExtractMessage_prefers_message_and_appends_detail()
    {
        var data = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["message"] = "Primary output",
            ["detail"] = "Extra details",
        };

        var result = GenerationResponseFormatter.ExtractMessage(data);

        Assert.Equal("Primary output" + Environment.NewLine + Environment.NewLine + "Extra details", result);
    }

    [Theory]
    [InlineData("result")]
    [InlineData("content")]
    [InlineData("text")]
    [InlineData("output")]
    public void ExtractMessage_reads_common_payload_keys_when_message_missing(string key)
    {
        var data = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            [key] = "Rendered preview text",
        };

        var result = GenerationResponseFormatter.ExtractMessage(data);

        Assert.Equal("Rendered preview text", result);
    }

    [Fact]
    public void ExtractMessage_falls_back_to_first_non_empty_string_value()
    {
        var data = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["provider"] = "openai",
            ["modelId"] = "gpt-4o-mini",
            ["misc"] = "",
        };

        var result = GenerationResponseFormatter.ExtractMessage(data);

        Assert.Equal("openai", result);
    }
}
