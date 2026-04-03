using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Godot_Generator_Avalonia.Services;
using Godot_Generator_Avalonia.ViewModels;
using Godot_Generator_Avalonia.Views;
using GodotGenerator.Api.Abstractions;
using GodotGenerator.Api.Services;
using GodotGenerator.Infrastructure.Ai.DependencyInjection;
using GodotGenerator.Infrastructure.Persistence.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace Godot_Generator_Avalonia;

/// <summary>
/// Avalonia application entry: loads configuration, composes DI, and shows the main window.
/// </summary>
public partial class App : Application
{
    /// <summary>Root service provider for the desktop host.</summary>
    public static IServiceProvider Services { get; private set; } = null!;

    /// <inheritdoc />
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging(builder =>
        {
            builder.AddDebug();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        services.AddGodotGeneratorPersistence(configuration);
        services.AddGodotGeneratorInfrastructure(configuration);
        services.AddScoped<IGodotGeneratorApiService, GodotGeneratorApiService>();
        services.AddScoped<IGeneratorApiClient, GeneratorApiClient>();
        services.AddSingleton<MainWindowViewModel>();

        Services = services.BuildServiceProvider();
    }

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();
            desktop.MainWindow = new MainWindow
            {
                DataContext = Services.GetRequiredService<MainWindowViewModel>(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}
