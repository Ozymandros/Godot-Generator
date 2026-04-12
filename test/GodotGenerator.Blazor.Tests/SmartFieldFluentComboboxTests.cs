using Bunit;
using GodotGenerator.Blazor.Client.Components.Generic;
using GodotGenerator.Blazor.Client.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.JSInterop;
using Xunit;

namespace GodotGenerator.Blazor.Tests;

/// <summary>
/// Ensures <see cref="SmartField"/> Select uses Fluent UI Blazor <see cref="FluentCombobox{T}"/> (library wraps the underlying web component).
/// </summary>
public sealed class SmartFieldFluentComboboxTests
{
    [Fact]
    public void SmartField_select_renders_fluent_combobox_host()
    {
        using var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ((IServiceCollection)ctx.Services).AddFluentUIComponents();

        var cut = ctx.Render<SmartField>(p => p
            .Add(x => x.FieldType, SmartFieldType.Select)
            .Add(x => x.SelectOptions, new[] { "alpha", "beta" })
            .Add(x => x.Placeholder, "—")
            .Add(x => x.Value, "alpha"));

        Assert.Contains("fluent-combobox", cut.Markup, StringComparison.Ordinal);
    }
}
