using Restaurant.Modules.Seating.Application;

namespace Restaurant.Api.Configuration;

public static class GuestMenuAccess
{
    public static IApplicationBuilder UseGuestMenuAccess(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        if ((HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method)) &&
            string.Equals(context.Request.Path.Value?.TrimEnd('/'), "/api/public/menu", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers.Pragma = "no-cache";
            var header = context.Request.Headers.Authorization.ToString();
            // The existing public menu is read-only. A supplied session credential must still be validated.
            if (header.Length == 0)
            {
                await next(context);
                return;
            }
            string? error = "qr.token.missing";
            if (header.StartsWith("Guest ", StringComparison.OrdinalIgnoreCase))
            {
                var access = context.RequestServices.GetRequiredService<IGuestAccess>();
                error = (await access.CheckAsync(header[6..], context.RequestAborted)).Error;
            }
            if (error is not null)
            {
                await Results.Problem(statusCode: StatusCodes.Status401Unauthorized,
                    title: "Guest session access is required.",
                    extensions: new Dictionary<string, object?> { ["code"] = error }).ExecuteAsync(context);
                return;
            }
        }
        await next(context);
    });
}
