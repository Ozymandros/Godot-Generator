using Godot_Generator_Avalonia.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace GodotGenerator.Ui.Tests;

public class MainWindowViewModelTests
{
    [Fact]
    public void NavItems_HaveExpectedCount()
    {
        var services = new ServiceCollection();
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        var vm = new MainWindowViewModel(scopeFactory);
        Assert.NotNull(vm.NavItems);
        Assert.Equal(9, vm.NavItems.Count);
    }
}
