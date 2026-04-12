using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GodotGenerator.Api.Dtos;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace GodotGenerator.Blazor.Tests;

/// <summary>
/// End-to-end HTTP tests against the hosted Blazor server (BFF only; API layer is <see cref="FakeGodotGeneratorApiService"/>).
/// </summary>
public sealed class BffHttpIntegrationTests : IClassFixture<GodotGeneratorBlazorWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _client;

    public BffHttpIntegrationTests(GodotGeneratorBlazorWebApplicationFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task Post_generate_text_returns_200_and_json_envelope()
    {
        var response = await _client.PostAsJsonAsync("/api/generate/text", new GenerateRequest("hello"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<Dictionary<string, object?>>>(JsonOptions);
        Assert.NotNull(body);
        Assert.True(body!.Success);
        Assert.Equal("fake", body.Data?["message"]?.ToString());
    }

    [Fact]
    public async Task Post_generate_animations_hits_animations_route()
    {
        var response = await _client.PostAsJsonAsync("/api/generate/animations", new GenerateRequest("idle"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<Dictionary<string, object?>>>(JsonOptions);
        Assert.Equal("animations", body?.Data?["modality"]?.ToString());
    }

    [Fact]
    public async Task Get_config_returns_200()
    {
        var response = await _client.GetAsync("/api/config");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
