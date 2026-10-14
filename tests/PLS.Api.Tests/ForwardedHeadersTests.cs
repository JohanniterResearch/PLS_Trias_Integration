using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace PLS.Api.Tests;

/// <summary>
/// Behind a reverse proxy (TRUSTED_PROXY), the auth rate limit must count per real client,
/// and only the trusted proxy may name that client.
/// </summary>
public class ProxyWebAppFactory : PlsWebAppFactory
{
    public const string TrustedProxy = "10.11.7.100";

    // TestServer has no TCP peer; this header stands in for the connection's source address.
    public const string PeerHeader = "X-Test-Peer";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("TRUSTED_PROXY", TrustedProxy);
        builder.UseSetting("RateLimiting:AuthPermitLimit", "10"); // the production default
        builder.ConfigureServices(services => services.AddTransient<IStartupFilter, PeerAddressStartupFilter>());
    }

    private sealed class PeerAddressStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, nextMiddleware) =>
            {
                if (IPAddress.TryParse(context.Request.Headers[PeerHeader], out var peer))
                    context.Connection.RemoteIpAddress = peer;
                await nextMiddleware();
            });
            next(app);
        };
    }
}

public class ForwardedHeadersTests : IClassFixture<ProxyWebAppFactory>
{
    // The default of the "auth" policy in Program.cs: 10 attempts per 60 s per client IP.
    private const int AuthPermitLimit = 10;

    private readonly HttpClient _client;

    public ForwardedHeadersTests(ProxyWebAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task BehindTrustedProxy_EachClientHasItsOwnLoginLimit()
    {
        for (var i = 0; i < AuthPermitLimit; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, await FailedLogin(ProxyWebAppFactory.TrustedProxy, "198.51.100.1"));

        Assert.Equal(HttpStatusCode.TooManyRequests, await FailedLogin(ProxyWebAppFactory.TrustedProxy, "198.51.100.1"));
        // Another client behind the same proxy is not locked out.
        Assert.Equal(HttpStatusCode.Unauthorized, await FailedLogin(ProxyWebAppFactory.TrustedProxy, "198.51.100.2"));
    }

    [Fact]
    public async Task TrustedProxyAsIPv4MappedPeer_IsStillTrusted()
    {
        // Kestrel on a dual-stack socket reports IPv4 peers as ::ffff:a.b.c.d.
        const string mappedProxy = "::ffff:" + ProxyWebAppFactory.TrustedProxy;
        for (var i = 0; i < AuthPermitLimit; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, await FailedLogin(mappedProxy, "198.51.100.11"));

        Assert.Equal(HttpStatusCode.TooManyRequests, await FailedLogin(mappedProxy, "198.51.100.11"));
        Assert.Equal(HttpStatusCode.Unauthorized, await FailedLogin(mappedProxy, "198.51.100.12"));
    }

    [Fact]
    public async Task UntrustedPeer_CannotEscapeTheLimitBySpoofingForwardedFor()
    {
        for (var i = 0; i < AuthPermitLimit; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, await FailedLogin("203.0.113.7", $"192.0.2.{i}"));

        Assert.Equal(HttpStatusCode.TooManyRequests, await FailedLogin("203.0.113.7", "192.0.2.200"));
    }

    private async Task<HttpStatusCode> FailedLogin(string peer, string forwardedFor)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/user-login")
        {
            Content = JsonContent.Create(new { username = "nobody", password = "wrong" })
        };
        request.Headers.Add(ProxyWebAppFactory.PeerHeader, peer);
        request.Headers.Add("X-Forwarded-For", forwardedFor);
        using var response = await _client.SendAsync(request);
        return response.StatusCode;
    }
}
