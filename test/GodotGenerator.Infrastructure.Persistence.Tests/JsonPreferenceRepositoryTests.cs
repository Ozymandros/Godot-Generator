#nullable enable
using GodotGenerator.Application.Abstractions;
using GodotGenerator.Infrastructure.Persistence.DependencyInjection;
using GodotGenerator.Infrastructure.Persistence.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GodotGenerator.Infrastructure.Persistence.Tests;

/// <summary>
/// Unit tests for <see cref="JsonPreferenceRepository"/> persistence behavior.
/// </summary>
public sealed class JsonPreferenceRepositoryTests
{
    /// <summary>
    /// Verifies set/get roundtrip persistence for a simple key-value preference.
    /// </summary>
    [Fact]
    public async Task Set_and_get_roundtrip()
    {
        var root = Path.Combine(Path.GetTempPath(), "gg-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [$"{DataStoreOptions.SectionName}:RootPath"] = root,
                })
                .Build();

            using var provider = new ServiceCollection()
                .AddLogging()
                .AddGodotGeneratorPersistence(configuration)
                .BuildServiceProvider();

            var repo = provider.GetRequiredService<IPreferenceRepository>();
            await repo.SetAsync("theme", "dark", CancellationToken.None);
            var value = await repo.GetAsync("theme", CancellationToken.None);
            Assert.Equal("dark", value);
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch
            {
                // best-effort cleanup
            }
        }
    }

    /// <summary>
    /// Verifies that assigning <see langword="null"/> removes an existing preference key.
    /// </summary>
    [Fact]
    public async Task Set_null_removes_existing_key()
    {
        var root = Path.Combine(Path.GetTempPath(), "gg-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            using var provider = BuildProvider(root);
            var repo = provider.GetRequiredService<IPreferenceRepository>();

            await repo.SetAsync("theme", "dark", CancellationToken.None);
            await repo.SetAsync("theme", null, CancellationToken.None);
            var value = await repo.GetAsync("theme", CancellationToken.None);

            Assert.Null(value);
        }
        finally
        {
            TryDelete(root);
        }
    }

    /// <summary>
    /// Verifies corrupted JSON input is handled gracefully by returning missing values.
    /// </summary>
    [Fact]
    public async Task Get_when_file_is_corrupted_returns_null_instead_of_throwing()
    {
        var root = Path.Combine(Path.GetTempPath(), "gg-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var filePath = Path.Combine(root, "preferences.json");
        await File.WriteAllTextAsync(filePath, "{ invalid-json", CancellationToken.None);

        try
        {
            using var provider = BuildProvider(root);
            var repo = provider.GetRequiredService<IPreferenceRepository>();

            var value = await repo.GetAsync("theme", CancellationToken.None);
            Assert.Null(value);
        }
        finally
        {
            TryDelete(root);
        }
    }

    /// <summary>
    /// Builds a service provider configured to use the specified temp root path.
    /// </summary>
    /// <param name="root">Temporary root directory for JSON persistence.</param>
    /// <returns>Configured service provider with persistence registrations.</returns>
    private static ServiceProvider BuildProvider(string root)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{DataStoreOptions.SectionName}:RootPath"] = root,
            })
            .Build();

        return new ServiceCollection()
            .AddLogging()
            .AddGodotGeneratorPersistence(configuration)
            .BuildServiceProvider();
    }

    /// <summary>
    /// Deletes a temporary directory using best-effort cleanup semantics.
    /// </summary>
    /// <param name="path">Directory path to remove.</param>
    private static void TryDelete(string path)
    {
        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
