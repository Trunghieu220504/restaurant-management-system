using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Restaurant.Modules.Reporting.Presentation;

public static class ReportingEndpoints
{
    public static RouteGroupBuilder MapReportingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Future endpoints must opt into anonymous access explicitly.
        return endpoints.MapGroup("/api/reporting")
            .WithTags("Reporting")
            .RequireAuthorization();
    }
}
