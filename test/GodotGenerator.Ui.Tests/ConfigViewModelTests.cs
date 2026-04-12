using Godot_Generator_Avalonia.Services;
using Godot_Generator_Avalonia.ViewModels;
using GodotGenerator.Api.Abstractions;
using GodotGenerator.Api.Dtos;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace GodotGenerator.Ui.Tests;

public class ConfigViewModelTests
{
    [Fact]
    public void Child_sections_are_initialized()
    {
        var services = new ServiceCollection();
        var mock = new Mock<IGodotGeneratorApiService>();
        mock.Setup(a => a.GetAllConfigAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<Dictionary<string, object?>>.Ok(new Dictionary<string, object?>
            {
                ["preferences"] = new Dictionary<string, string?>(),
                ["keys"] = new List<string>(),
                ["providers"] = new List<object>
                {
                    new Dictionary<string, object?> { ["name"] = "x", ["defaultChatModelId"] = "y" },
                },
                ["models"] = new Dictionary<string, object?>(),
                ["prompts"] = new Dictionary<string, object?>(),
                ["godotToolNames"] = new List<string>(),
            }));
        services.AddScoped(_ => mock.Object);
        services.AddScoped<IGeneratorApiClient, GeneratorApiClient>();
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        var vm = new ConfigViewModel(scopeFactory);

        Assert.NotNull(vm.General);
        Assert.NotNull(vm.Providers);
        Assert.NotNull(vm.Models);
        Assert.NotNull(vm.Prompts);
        Assert.NotNull(vm.Secrets);
    }
}
