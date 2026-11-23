using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Restaurant.Modules.Audit.Application;
using System.Globalization;

namespace Restaurant.Modules.Audit.Presentation;

public static class AuditEndpoints
{
    public static RouteGroupBuilder MapAuditEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/audit")
            .WithTags("Audit")
            .RequireAuthorization();
        group.MapGet("/logs", ReadAsync)
            .RequireAuthorization(policy => policy.RequireRole("Manager"))
            .WithName("GetAuditLogs")
            .WithSummary("Read a page of audit records (Manager only).")
            .WithDescription("Filters combine with AND. fromUtc is inclusive, toUtc exclusive; timestamps must be UTC. Ordered by occurredAtUtc then eventId descending. page defaults to 1; pageSize defaults to 20 and is capped at 100. No write endpoint is exposed.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);
        return group;
    }

    private static async Task<Results<Ok<AuditLogPage>, ProblemHttpResult>> ReadAsync(
        HttpContext context, IAuditQueries queries, CancellationToken cancellationToken,
        string? actorId = null, string? entityType = null, string? entityId = null,
        string? fromUtc = null, string? toUtc = null, string? page = null, string? pageSize = null)
    {
        context.Response.Headers.CacheControl = "no-store";
        string[] allowed = ["actorId", "entityType", "entityId", "fromUtc", "toUtc", "page", "pageSize"];
        var valid = context.Request.Query.All(x => allowed.Contains(x.Key, StringComparer.Ordinal) && x.Value.Count == 1);
        Guid? actor = null;
        if (actorId is not null)
        {
            valid &= Guid.TryParseExact(actorId, "D", out var parsed);
            actor = parsed;
        }
        valid &= TryUtc(fromUtc, out var from) & TryUtc(toUtc, out var to);
        var pageNumber = 1;
        var size = 20;
        if (page is not null) valid &= int.TryParse(page, NumberStyles.None, CultureInfo.InvariantCulture, out pageNumber);
        if (pageSize is not null) valid &= int.TryParse(pageSize, NumberStyles.None, CultureInfo.InvariantCulture, out size);
        var query = new AuditLogQuery(actor, entityType, entityId, from, to, pageNumber, size);
        if (!valid || !query.IsValid)
            return TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid audit query.", extensions: new Dictionary<string, object?> { ["code"] = "audit.query.invalid" });
        return TypedResults.Ok(await queries.FindAsync(query, cancellationToken));
    }

    private static bool TryUtc(string? value, out DateTimeOffset? result)
    {
        result = null;
        if (value is null) return true;
        if (!DateTimeOffset.TryParseExact(value,
            ["yyyy-MM-dd'T'HH:mm:ss'Z'", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'", "yyyy-MM-dd'T'HH:mm:sszzz", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz"],
            CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var timestamp) || timestamp.Offset != TimeSpan.Zero) return false;
        result = timestamp;
        return true;
    }
}
