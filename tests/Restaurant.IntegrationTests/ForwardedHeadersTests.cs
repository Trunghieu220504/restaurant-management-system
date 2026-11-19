using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Restaurant.IntegrationTests;

public sealed class ForwardedHeadersTests
{
    [Fact]
    public void Invalid_proxy_address_prevents_startup()
    {
        using var factory = CreateFactory("10.77.0.10", "not-an-ip");
        var error = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.Contains("ReverseProxy:KnownProxies", error.Message);
    }

    [Fact]
    public async Task Asymmetric_headers_do_not_change_peer_or_scheme()
    {
        using var factory = CreateFactory("10.77.0.10", "10.77.0.10");
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("X-Forwarded-For", "203.0.113.9, 198.51.100.1");
        request.Headers.Add("X-Forwarded-Proto", "https");
        using var response = await client.SendAsync(request);
        Assert.Equal("10.77.0.10", Assert.Single(response.Headers.GetValues("Test-Peer")));
        Assert.Equal("http", Assert.Single(response.Headers.GetValues("Test-Scheme")));
    }

    [Theory]
    [InlineData("10.77.0.10", HttpStatusCode.OK)]
    [InlineData("10.77.0.11", HttpStatusCode.TemporaryRedirect)]
    public async Task Forwarded_scheme_is_applied_before_https_redirect(string peer, HttpStatusCode expected)
    {
        using var factory = CreateFactory(peer, "10.77.0.10", staging: true);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("X-Forwarded-For", "198.51.100.1");
        request.Headers.Add("X-Forwarded-Proto", "https");
        using var response = await client.SendAsync(request);
        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData("10.77.0.10", "https", "198.51.100.1")]
    [InlineData("10.77.0.11", "http", "10.77.0.11")]
    public async Task Only_trusted_peer_can_forward_scheme_and_address(string peer, string scheme, string ip)
    {
        using var factory = CreateFactory(peer, "10.77.0.10");
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("X-Forwarded-For", "198.51.100.1");
        request.Headers.Add("X-Forwarded-Proto", "https");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(scheme, Assert.Single(response.Headers.GetValues("Test-Scheme")));
        Assert.Equal(ip, Assert.Single(response.Headers.GetValues("Test-Peer")));
    }

    [Fact]
    public async Task Only_nearest_forwarded_hop_is_used()
    {
        using var factory = CreateFactory("10.77.0.10", "10.77.0.10");
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("X-Forwarded-For", "203.0.113.9, 198.51.100.1");
        request.Headers.Add("X-Forwarded-Proto", "http, https");
        using var response = await client.SendAsync(request);
        Assert.Equal("198.51.100.1", Assert.Single(response.Headers.GetValues("Test-Peer")));
        Assert.Equal("https", Assert.Single(response.Headers.GetValues("Test-Scheme")));
    }

    [Fact]
    public async Task Trusted_proxy_separates_login_limits_by_client_ip()
    {
        using var factory = CreateFactory("10.77.0.10", "10.77.0.10");
        using var client = factory.CreateClient();
        for (var i = 0; i < 10; i++)
            Assert.Equal(HttpStatusCode.BadRequest, await Login(client, "198.51.100.1"));
        Assert.Equal(HttpStatusCode.TooManyRequests, await Login(client, "198.51.100.1"));
        Assert.Equal(HttpStatusCode.BadRequest, await Login(client, "198.51.100.2"));
    }

    [Theory]
    [InlineData("10.77.0.11", "10.77.0.10")]
    [InlineData("127.0.0.1", null)]
    public async Task Untrusted_peer_cannot_evade_login_limit_by_spoofing_headers(string peer, string? trusted)
    {
        using var factory = CreateFactory(peer, trusted);
        using var client = factory.CreateClient();
        for (var i = 1; i <= 10; i++)
            Assert.Equal(HttpStatusCode.BadRequest, await Login(client, $"198.51.100.{i}"));
        Assert.Equal(HttpStatusCode.TooManyRequests, await Login(client, "198.51.100.11"));
    }

    private static WebApplicationFactory<Program> CreateFactory(string peer, string? trusted, bool staging = false) =>
        new ApiFactory().WithWebHostBuilder(builder =>
        {
            if (staging) builder.UseEnvironment("Staging");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                trusted is null ? new Dictionary<string, string?>()
                    : new Dictionary<string, string?> { ["ReverseProxy:KnownProxies:0"] = trusted }));
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IStartupFilter>(new PeerAddressFilter(peer));
                services.Configure<Microsoft.AspNetCore.HttpsPolicy.HttpsRedirectionOptions>(options => options.HttpsPort = 443);
            });
        });

    private static async Task<HttpStatusCode> Login(HttpClient client, string ip)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/identity/login")
        {
            Content = JsonContent.Create(new { userName = "", password = "" })
        };
        request.Headers.Add("X-Forwarded-For", ip);
        request.Headers.Add("X-Forwarded-Proto", "https");
        using var response = await client.SendAsync(request);
        return response.StatusCode;
    }

    private sealed class PeerAddressFilter(string peer) : IStartupFilter
    {
        public Action<Microsoft.AspNetCore.Builder.IApplicationBuilder> Configure(
            Action<Microsoft.AspNetCore.Builder.IApplicationBuilder> next) => app =>
        {
            app.Use((context, pipeline) =>
            {
                context.Connection.RemoteIpAddress = IPAddress.Parse(peer);
                context.Response.OnStarting(() =>
                {
                    context.Response.Headers["Test-Scheme"] = context.Request.Scheme;
                    context.Response.Headers["Test-Peer"] = context.Connection.RemoteIpAddress?.ToString();
                    return Task.CompletedTask;
                });
                return pipeline(context);
            });
            next(app);
        };
    }
}
