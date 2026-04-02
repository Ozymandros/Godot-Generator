using GodotGenerator.Application.Abstractions;
using GodotGenerator.Infrastructure.Persistence.Json;
using GodotGenerator.Infrastructure.Persistence.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GodotGenerator.Infrastructure.Persistence.DependencyInjection;

/// <summary>
/// Registers JSON file persistence adapters.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds JSON-backed persistence (preferences, etc.) using <see cref="DataStoreOptions"/>.
    /// </summary>
    public static IServiceCollection AddGodotGeneratorPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        services
            .AddOptions<DataStoreOptions>()
            .Bind(configuration.GetSection(DataStoreOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.RootPath),
                $"{DataStoreOptions.SectionName}:RootPath is required.")
            .ValidateOnStart();
        services.AddSingleton<IPreferenceRepository, JsonPreferenceRepository>();
        return services;
    }
}
