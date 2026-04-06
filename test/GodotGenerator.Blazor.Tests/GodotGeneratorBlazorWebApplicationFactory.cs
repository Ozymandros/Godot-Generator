using GodotGenerator.Api.Abstractions;
using GodotGenerator.Blazor;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GodotGenerator.Blazor.Tests;

/// <summary>
/// Host factory for HTTP-level tests: swaps <see cref="IGodotGeneratorApiService"/> with <see cref="FakeGodotGeneratorApiService"/>
/// so the real AI/MCP stack does not run.
/// </summary>
public sealed class GodotGeneratorBlazorWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll(typeof(IGodotGeneratorApiService));
            services.AddSingleton<IGodotGeneratorApiService, FakeGodotGeneratorApiService>();
        });
    }
}
