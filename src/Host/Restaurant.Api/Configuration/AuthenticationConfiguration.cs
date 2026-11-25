using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Restaurant.Modules.Identity.Application.Authentication;

namespace Restaurant.Api.Configuration;

public static class AuthenticationConfiguration
{
    public static IServiceCollection AddApiAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();
        services.AddSingleton(PartitionedRateLimiter.Create<string, string>(table =>
            RateLimitPartition.GetFixedWindowLimiter(table, _ => new FixedWindowRateLimiterOptions
            { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })));
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, ct) =>
            {
                var response = context.HttpContext.Response;
                if (!context.HttpContext.Request.Path.StartsWithSegments("/api/public/guest-access") &&
                    !string.Equals(context.HttpContext.Request.Path.Value?.TrimEnd('/'), "/api/public/menu", StringComparison.OrdinalIgnoreCase)) return;
                var retry = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var delay) ? delay.TotalSeconds : 60;
                response.Headers.RetryAfter = ((int)Math.Ceiling(retry)).ToString(System.Globalization.CultureInfo.InvariantCulture);
                response.Headers.CacheControl = "no-store";
                await Results.Problem(statusCode: 429, extensions: new Dictionary<string, object?> { ["code"] = "qr.rate.limited" }).ExecuteAsync(context.HttpContext);
            };
            options.AddPolicy("qr-exchange", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            options.AddPolicy("qr-read", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 240, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            options.AddPolicy("qr-submit", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            options.AddPolicy("identity-login", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            options.AddPolicy("identity-refresh", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            options.AddPolicy("identity-password", context => RateLimitPartition.GetFixedWindowLimiter(
                context.User.FindFirst("sub")?.Value ?? "anonymous",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });
        services.AddOptions<JwtSettings>()
            .Bind(configuration.GetSection(JwtSettings.SectionName))
            .Validate(settings => settings.IsValid(),
                "Configure Jwt:Issuer, Jwt:Audience and a signing key with at least 32 UTF-8 bytes.")
            .ValidateOnStart();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtSettings>>((options, settings) =>
            {
                var jwt = settings.Value;
                options.MapInboundClaims = false;
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        if (context.Request.Path == "/hubs/operations" &&
                            context.Request.Query.TryGetValue("access_token", out var token) && token.Count == 1 &&
                            (context.HttpContext.WebSockets.IsWebSocketRequest ||
                             context.Request.Headers.Accept.ToString().Contains("text/event-stream", StringComparison.OrdinalIgnoreCase)))
                            context.Token = token;
                        return Task.CompletedTask;
                    },
                    OnTokenValidated = async context =>
                    {
                        var principal = context.Principal!;
                        if (principal.FindAll("sub").Count() != 1 || principal.FindAll("sid").Count() != 1 ||
                            !Guid.TryParse(principal.FindFirst("sub")?.Value, out var userId) ||
                            !Guid.TryParse(principal.FindFirst("sid")?.Value, out var sessionId))
                        {
                            context.Fail("Invalid account session.");
                            return;
                        }
                        var service = context.HttpContext.RequestServices.GetRequiredService<IIdentityAuthentication>();
                        var account = await service.ValidateSessionAsync(userId, sessionId, context.HttpContext.RequestAborted);
                        if (account is null)
                        {
                            context.Fail("Invalid account session.");
                            return;
                        }
                        var identity = (ClaimsIdentity)principal.Identity!;
                        foreach (var claim in identity.FindAll("role").Concat(identity.FindAll("permission")).ToArray())
                            identity.RemoveClaim(claim);
                        identity.AddClaims(account.Roles.Select(role => new Claim("role", role)));
                        identity.AddClaims(account.Permissions.Select(code => new Claim("permission", code)));
                        context.HttpContext.Items[typeof(CurrentAccount)] = account;
                    }
                };
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    RequireSignedTokens = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "sub",
                    RoleClaimType = "role"
                };
            });

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build())
            .AddPolicy("SystemAdministration", policy => policy.RequireRole("Admin"))
            .AddPermissionPolicies();

        return services;
    }
}
