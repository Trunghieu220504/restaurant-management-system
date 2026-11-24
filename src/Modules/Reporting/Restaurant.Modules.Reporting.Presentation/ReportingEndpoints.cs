using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using Restaurant.Modules.Reporting.Application;

namespace Restaurant.Modules.Reporting.Presentation;

public static class ReportingEndpoints
{
    public static RouteGroupBuilder MapReportingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/reporting").WithTags("Reporting")
            .RequireAuthorization(p => p.RequireRole("Manager").RequireClaim("permission", "reporting.read"));
        group.MapGet("/daily", (DateOnly date, IBusinessReports reports, CancellationToken ct) => reports.ReadAsync(date, date, ct))
            .WithName("DailyBusinessReport").Produces<BusinessReport>();
        group.MapGet("/range", (DateOnly from, DateOnly to, IBusinessReports reports, CancellationToken ct) =>
            Range(from, to, reports.ReadAsync, ct)).WithName("RangeBusinessReport").Produces<BusinessReport>().ProducesProblem(400);
        group.MapGet("/items-sold", (DateOnly from, DateOnly to, IBusinessReports reports, CancellationToken ct) =>
            Range(from, to, reports.ItemsAsync, ct)).WithName("ItemsSoldReport").Produces<IReadOnlyList<SoldItem>>().ProducesProblem(400);
        group.MapGet("/stock", (DateOnly from, DateOnly to, IBusinessReports reports, CancellationToken ct) =>
            Range(from, to, reports.StockAsync, ct)).WithName("ClosingStockReport").Produces<IReadOnlyList<ClosingStock>>().ProducesProblem(400);
        return group;
    }

    private static async Task<IResult> Range<T>(DateOnly from, DateOnly to, Func<DateOnly, DateOnly, CancellationToken, Task<T>> query, CancellationToken ct) =>
        to < from ? Results.Problem(statusCode: 400, title: "reporting.range.invalid",
            extensions: new Dictionary<string, object?> { ["code"] = "reporting.range.invalid" }) : Results.Ok(await query(from, to, ct));
}
