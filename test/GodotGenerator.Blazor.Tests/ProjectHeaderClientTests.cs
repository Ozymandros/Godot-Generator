using Bunit;
using GodotGenerator.Blazor.Client.Components.Layout;
using GodotGenerator.Blazor.Client.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.JSInterop;
using Xunit;

namespace GodotGenerator.Blazor.Tests;

/// <summary>
/// Component tests for <see cref="ProjectHeaderClient"/> (browse / Electron messaging).
/// </summary>
public sealed class ProjectHeaderClientTests
{
    private const string BrowseHintFragment = "Folder picker is only available";

    [Fact]
    public async Task Browse_outside_electron_shows_info_message()
    {
        using var ctx = CreateConfiguredContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.JSInterop.Setup<string?>("godotElectronInterop.pickFolder").SetResult(null);
        ctx.JSInterop.Setup<bool>("godotElectronInterop.isElectron").SetResult(false);

        RegisterProjectState(ctx);

        var cut = ctx.Render<ProjectHeaderClient>();

        await cut.InvokeAsync(async () =>
        {
            await cut.FindAll("fluent-button")[0].ClickAsync(new MouseEventArgs());
        });

        cut.WaitForAssertion(() => Assert.Contains(BrowseHintFragment, cut.Markup, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Browse_in_electron_cancelled_does_not_show_info_message()
    {
        using var ctx = CreateConfiguredContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.JSInterop.Setup<string?>("godotElectronInterop.pickFolder").SetResult(null);
        ctx.JSInterop.Setup<bool>("godotElectronInterop.isElectron").SetResult(true);

        RegisterProjectState(ctx);

        var cut = ctx.Render<ProjectHeaderClient>();

        await cut.InvokeAsync(async () =>
        {
            await cut.FindAll("fluent-button")[0].ClickAsync(new MouseEventArgs());
        });

        Assert.DoesNotContain(BrowseHintFragment, cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Browse_selected_path_updates_field()
    {
        using var ctx = CreateConfiguredContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.JSInterop.Setup<string?>("godotElectronInterop.pickFolder").SetResult(@"C:\Games\Demo");

        RegisterProjectState(ctx);

        var cut = ctx.Render<ProjectHeaderClient>();

        await cut.InvokeAsync(async () =>
        {
            await cut.FindAll("fluent-button")[0].ClickAsync(new MouseEventArgs());
        });

        cut.WaitForAssertion(() => Assert.Contains(@"C:\Games\Demo", cut.Markup, StringComparison.Ordinal));
    }

    private static BunitContext CreateConfiguredContext()
    {
        var ctx = new BunitContext();
        ((IServiceCollection)ctx.Services).AddFluentUIComponents();
        return ctx;
    }

    private static void RegisterProjectState(BunitContext ctx)
    {
        ((IServiceCollection)ctx.Services).AddScoped<ProjectStateService>(sp =>
            new ProjectStateService(sp.GetRequiredService<IJSRuntime>()));
    }
}
