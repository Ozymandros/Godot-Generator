#nullable enable
using System.Reflection;
using GodotGenerator.Infrastructure.Ai.Services;
using Xunit;

namespace GodotGenerator.Infrastructure.Ai.Tests;

public sealed class GodotAutoInvokeFilterTests
{
    [Fact]
    public void IsLikelyGdScript_detects_real_gdscript_block()
    {
        var code = @"extends Node
class_name MyNode
func _ready():
    print(""hello"")
";

        var method = typeof(GodotAutoInvokeFilter).GetMethod("IsLikelyGdScript", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        var result = (bool)method.Invoke(null, new object[] { code })!;
        Assert.True(result);
    }

    [Fact]
    public void IsLikelyGdScript_rejects_single_token_snippet()
    {
        var code = "var foo = 1";
        var method = typeof(GodotAutoInvokeFilter).GetMethod("IsLikelyGdScript", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        var result = (bool)method.Invoke(null, new object[] { code })!;
        Assert.False(result);
    }

    [Fact]
    public void IsLikelyGdScript_rejects_non_gd_code()
    {
        var code = "public class C { int x = 0; }";
        var method = typeof(GodotAutoInvokeFilter).GetMethod("IsLikelyGdScript", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        var result = (bool)method.Invoke(null, new object[] { code })!;
        Assert.False(result);
    }

    [Fact]
    public void IsLikelyCSharp_detects_csharp_block()
    {
        var code = @"using Godot;
using System;
public partial class MyNode : Node
{
    public override void _Ready()
    {
        GD.Print(""hello"");
    }
}"
        ;
        var method = typeof(GodotAutoInvokeFilter).GetMethod("IsLikelyCSharp", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        var result = (bool)method.Invoke(null, new object[] { code })!;
        Assert.True(result);
    }

    [Fact]
    public void IsLikelyCSharp_rejects_non_csharp()
    {
        var code = "extends Node\nfunc _ready():\n    pass";
        var method = typeof(GodotAutoInvokeFilter).GetMethod("IsLikelyCSharp", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        var result = (bool)method.Invoke(null, new object[] { code })!;
        Assert.False(result);
    }
}
