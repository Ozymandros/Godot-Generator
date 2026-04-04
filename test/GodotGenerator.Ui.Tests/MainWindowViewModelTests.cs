using Godot_Generator_Avalonia.Services;
using Godot_Generator_Avalonia.ViewModels;
using GodotGenerator.Api.Abstractions;
using GodotGenerator.Api.Dtos;
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
        RegisterMinimalApiMocks(services);
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        var vm = new MainWindowViewModel(scopeFactory);
        Assert.NotNull(vm.NavItems);
        Assert.Equal(12, vm.NavItems.Count);
    }

    [Fact]
    public void SelectNavigationCommand_ChangesSelectedIndex()
    {
        var services = new ServiceCollection();
        RegisterMinimalApiMocks(services);
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        var vm = new MainWindowViewModel(scopeFactory);

        vm.ShowAnimationsCommand.Execute(null);

        Assert.Equal(6, vm.SelectedNavIndex);
    }

    private static void RegisterMinimalApiMocks(ServiceCollection services)
    {
        var mock = new Mock<IGodotGeneratorApiService>();
        mock.Setup(a => a.GetAllConfigAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<Dictionary<string, object?>>.Ok(new Dictionary<string, object?>
            {
                ["preferences"] = new Dictionary<string, string?>(),
                ["keys"] = new List<string>(),
                ["providers"] = new List<object>
                {
                    new Dictionary<string, object?> { ["name"] = "openai", ["defaultChatModelId"] = "gpt-4o-mini" },
                },
                ["models"] = new Dictionary<string, object?>(),
                ["prompts"] = new Dictionary<string, object?>(),
                ["godotToolNames"] = new List<string>(),
            }));
        services.AddScoped(_ => mock.Object);
        services.AddScoped<IGeneratorApiClient, GeneratorApiClient>();
    }
}
