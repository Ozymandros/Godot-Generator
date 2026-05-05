#nullable enable
using System.IO;
using GodotGenerator.Application.Abstractions;
using GodotGenerator.Infrastructure.Persistence.Json;
using GodotGenerator.Infrastructure.Persistence.Options;
using GodotGenerator.Infrastructure.Persistence.Security;
using Microsoft.AspNetCore.DataProtection;
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
    /// API key values are encrypted at rest via ASP.NET Core Data Protection with OS-native
    /// key storage (DPAPI on Windows, Keychain-backed on macOS, XDG on Linux).
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

        // Data Protection with OS-native key storage, scoped to this application so keys
        // are not shared with other apps on the same machine.
        services.AddDataProtection()
            .SetApplicationName("GodotGenerator")
            .PersistKeysToFileSystem(new DirectoryInfo(ResolveDataProtectionKeysPath()));

        // Internal implementation registrations.
        services.AddSingleton<JsonPreferenceRepository>();
        services.AddSingleton<ISecretProtector, DataProtectionSecretProtector>();

        // Public IPreferenceRepository is the encrypting decorator backed by the JSON store.
        services.AddSingleton<IPreferenceRepository, SecurePreferenceRepository>();

        return services;
    }

    /// <summary>
    /// Resolves the directory used to persist Data Protection key material.
    /// Uses <c>%LOCALAPPDATA%/GodotGenerator/dp-keys</c> so keys survive app updates.
    /// </summary>
    private static string ResolveDataProtectionKeysPath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var root = string.IsNullOrWhiteSpace(localAppData)
            ? AppContext.BaseDirectory
            : localAppData;
        return Path.Combine(root, "GodotGenerator", "dp-keys");
    }
}
