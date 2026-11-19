using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace Restaurant.Api.Configuration;

public static class ReverseProxyConfiguration
{
    public static IServiceCollection AddTrustedReverseProxy(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            var proxies = configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [];
            var addresses = proxies.Select(value => IPAddress.TryParse(value, out var address)
                ? address
                : throw new InvalidOperationException("ReverseProxy:KnownProxies must contain valid IP addresses.")).ToArray();
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();
            // An empty trust list otherwise means trust every peer in the framework.
            options.ForwardedHeaders = addresses.Length == 0 ? ForwardedHeaders.None
                : ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;
            options.RequireHeaderSymmetry = true;
            foreach (var address in addresses) options.KnownProxies.Add(address);
        });
        return services;
    }
}
