using Godot_Generator_Avalonia.Models;
using Godot_Generator_Avalonia.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GodotGenerator.Ui.Tests;

public class GenerationPanelViewModelTests
{
    [Fact]
    public void DisplayTitle_MapsNewGodotModalities()
    {
        var services = new ServiceCollection();
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        var vmScenes = new GenerationPanelViewModel(scopeFactory, GenerationModality.Scenes);
        var vmProject = new GenerationPanelViewModel(scopeFactory, GenerationModality.GodotProject);
        var vmAnimations = new GenerationPanelViewModel(scopeFactory, GenerationModality.Animations);

        Assert.Equal("Create Scene", vmScenes.DisplayTitle);
        Assert.Equal("Create Godot Project", vmProject.DisplayTitle);
        Assert.Equal("Godot Animations", vmAnimations.DisplayTitle);
    }

    [Fact]
    public void LabelsAndWatermarks_ReflectModality()
    {
        var services = new ServiceCollection();
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        var vm = new GenerationPanelViewModel(scopeFactory, GenerationModality.Animations);

        Assert.Equal("Animation Description", vm.PromptLabel);
        Assert.Contains("keyframes", vm.PromptWatermark);
    }

    [Fact]
    public void AdvancedOverrides_InitialState()
    {
        var services = new ServiceCollection();
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        var vm = new GenerationPanelViewModel(scopeFactory, GenerationModality.Text);

        Assert.Equal(0.7, vm.Temperature);
        Assert.Null(vm.ApiKeyOverride);
        Assert.Null(vm.SystemPromptOverride);
    }
}
