using Bunit;
using GodotGenerator.Blazor.Client.Components.Panels.Godot;
using GodotGenerator.Blazor.Client.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.JSInterop;
using Xunit;

namespace GodotGenerator.Blazor.Tests;

/// <summary>
/// bUnit coverage for Godot MCP modality surfacing (Home, hub page, shared panel controls).
/// </summary>
public sealed class GodotMcpUiSurfacingTests
{
    [Fact]
    public void Home_includes_godot_mcp_generation_links()
    {
        using var ctx = CreateFluentContext();
        var cut = ctx.Render<Home>();

        Assert.Contains("href=\"/generate/godot-lighting\"", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("href=\"/generate/godot-camera\"", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("href=\"/generate/godot-shaders\"", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("href=\"/generate/godot-signals\"", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("href=\"/generate/godot-nodes\"", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("href=\"/generate/godot-ui\"", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("href=\"/generate/godot-physics\"", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("href=\"/generate/godot-mcp\"", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void GodotMcpToolsPage_lists_modality_anchors()
    {
        using var ctx = CreateFluentContext();
        var cut = ctx.Render<GodotMcpToolsPage>();

        Assert.Contains("href=\"/generate/godot-lighting\"", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("href=\"/generate/godot-camera\"", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("href=\"/generate/godot-shaders\"", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("href=\"/generate/godot-signals\"", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("href=\"/generate/godot-nodes\"", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("href=\"/generate/godot-ui\"", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("href=\"/generate/godot-physics\"", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void GodotProjectValidationSwitch_renders_mcp_validation_text()
    {
        using var ctx = CreateFluentContext();
        var cut = ctx.Render<GodotProjectValidationSwitch>(p => p
            .Add(x => x.Value, true)
            .Add(x => x.ValueChanged, EventCallback.Factory.Create<bool>(new object(), _ => { })));

        Assert.Contains("MCP validation", cut.Markup, StringComparison.Ordinal);
    }

    private static BunitContext CreateFluentContext()
    {
        var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ((IServiceCollection)ctx.Services).AddFluentUIComponents();
        return ctx;
    }
}
