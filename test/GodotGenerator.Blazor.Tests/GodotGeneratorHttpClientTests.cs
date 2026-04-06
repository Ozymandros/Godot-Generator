using System.Net;
using System.Text;
using System.Text.Json;
using GodotGenerator.Api.Dtos;
using GodotGenerator.Blazor.Client.Models;
using GodotGenerator.Blazor.Client.Services;
using Xunit;

namespace GodotGenerator.Blazor.Tests;

/// <summary>
/// Tests <see cref="GodotGeneratorHttpClient"/> JSON handling against a stub <see cref="HttpMessageHandler"/>.
/// </summary>
public sealed class GodotGeneratorHttpClientTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(_respond(request));
    }

    [Fact]
    public async Task GenerateAsync_posts_to_expected_route_for_each_modality()
    {
        string? lastUri = null;
        var handler = new StubHandler(req =>
        {
            lastUri = req.RequestUri?.PathAndQuery;
            var json = JsonSerializer.Serialize(
                ApiResponse<Dictionary<string, object?>>.Ok(new Dictionary<string, object?> { ["message"] = "ok" }),
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
        });

        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var sut = new GodotGeneratorHttpClient(http);

        _ = await sut.GenerateAsync(GenerationModality.Animations, new GenerateRequest("x"), CancellationToken.None);

        Assert.Equal("/api/generate/animations", lastUri);
    }

    [Fact]
    public async Task PostGenerateAsync_returns_Fail_when_body_empty()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(string.Empty, Encoding.UTF8, "application/json"),
        });
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var sut = new GodotGeneratorHttpClient(http);

        var r = await sut.PostGenerateAsync("api/generate/text", new GenerateRequest("a"), CancellationToken.None);

        Assert.False(r.Success);
        Assert.Equal("Empty response.", r.Error);
    }

    [Fact]
    public async Task PostGenerateAsync_returns_Fail_when_body_is_not_json()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("not-json", Encoding.UTF8, "application/json"),
        });
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var sut = new GodotGeneratorHttpClient(http);

        var r = await sut.PostGenerateAsync("api/generate/text", new GenerateRequest("a"), CancellationToken.None);

        Assert.False(r.Success);
        Assert.Equal("Invalid JSON response.", r.Error);
    }
}
