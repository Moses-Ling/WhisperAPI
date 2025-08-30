using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using WhisperAPI.Configuration;

namespace WhisperAPI.Tests;

public class BasicEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public BasicEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(_ => { });
    }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task Models_ReturnsList()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/v1/models");
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync();
        Assert.Contains("\"object\":\"list\"", json);
        Assert.Contains("whisper-base", json);
    }

    [Fact]
    public async Task Config_ReturnsDefaults()
    {
        var client = _factory.CreateClient();
        var cfg = await client.GetFromJsonAsync<AppConfiguration>("/config");
        Assert.NotNull(cfg);
        Assert.Equal("whisper-base", cfg!.Whisper.ModelName);
        Assert.Equal(8000, cfg.Server.Port);
    }
}
