#nullable enable
using System.Text.Json;
using GodotGenerator.Application.Serialization;
using Xunit;

namespace GodotGenerator.Application.Tests;

public sealed class JsonOptionValueTests
{
    [Fact]
    public void AsTrimmedString_reads_json_element_string()
    {
        var el = JsonDocument.Parse("\"C:\\\\Games\\\\MyProject\"").RootElement;
        Assert.Equal(@"C:\Games\MyProject", JsonOptionValue.AsTrimmedString(el));
    }

    [Fact]
    public void IsNullOrEmptyStringLike_true_for_json_empty_string()
    {
        var el = JsonDocument.Parse("\"\"").RootElement;
        Assert.True(JsonOptionValue.IsNullOrEmptyStringLike(el));
    }
}
