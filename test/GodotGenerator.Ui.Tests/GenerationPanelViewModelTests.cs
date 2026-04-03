using Godot_Generator_Avalonia.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GodotGenerator.Ui.Tests;

public class GenerationPanelViewModelTests
{
    [Fact]
    public void DisplayTitle_MapsGodotUiAndPhysics()
    {
        var services = new ServiceCollection();
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        var vmUi = new GenerationPanelViewModel(scopeFactory, Models.GenerationModality.GodotUi);
        var vmPhysics = new GenerationPanelViewModel(scopeFactory, Models.GenerationModality.GodotPhysics);

        Assert.Equal("Godot UI", vmUi.DisplayTitle);
        Assert.Equal("Godot Physics", vmPhysics.DisplayTitle);
    }
}
