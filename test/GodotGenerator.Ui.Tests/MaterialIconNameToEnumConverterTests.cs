using Godot_Generator_Avalonia.Converters;
using Xunit;

namespace GodotGenerator.Ui.Tests;

public class MaterialIconNameToEnumConverterTests
{
    [Theory]
    [InlineData("settings")]
    [InlineData("Text")]
    [InlineData("text_fields")]
    [InlineData("CommentTextOutline")]
    public void Convert_ReturnsNonNull_ForKnownKeys(string key)
    {
        var conv = new MaterialIconNameToEnumConverter();
        var res = conv.Convert(key, typeof(object), null, System.Globalization.CultureInfo.InvariantCulture);
        Assert.NotNull(res);
    }
}
