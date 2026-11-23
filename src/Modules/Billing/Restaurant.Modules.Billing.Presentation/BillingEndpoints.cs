using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Restaurant.Modules.Billing.Application;

namespace Restaurant.Modules.Billing.Presentation;

public static class BillingEndpoints
{
    public static RouteGroupBuilder MapBillingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var billing = endpoints.MapGroup("/api/billing").WithTags("Billing").RequireAuthorization();
        var read = billing.MapGroup("").RequireAuthorization(policy => policy.RequireClaim("permission",
            "billing.payments.collect", "billing.waivers.approve"));
        read.MapGet("/invoices/{id:long}", async (long id, IBillingLedger ledger, CancellationToken ct) =>
            await ledger.FindAsync(id, ct) is { } invoice ? Results.Ok(invoice) : Problem("billing.invoice.not_found", 404))
            .WithName("BillingInvoice").Produces<InvoiceView>();
        read.MapGet("/orders/{orderId:long}/invoice", async (long orderId, IBillingLedger ledger, CancellationToken ct) =>
            await ledger.ForOrderAsync(orderId, ct) is { } id ? Results.Ok(await ledger.FindAsync(id, ct)) : Problem("billing.invoice.not_found", 404))
            .WithName("BillingOrderInvoice").Produces<InvoiceView>();
        read.MapGet("/documents/{id:long}", async (long id, IBillingLedger ledger, CancellationToken ct) =>
            await ledger.DocumentAsync(id, ct) is { } json ? Results.Content(json, "application/json") : Problem("billing.document.not_found", 404))
            .WithName("BillingDocument").Produces<PrintableBill>();
        var collect = billing.MapGroup("/invoices/{id:long}/payments").RequireAuthorization(policy => policy.RequireClaim("permission", "billing.payments.collect"));
        collect.MapPost("", (long id, CollectPayment body, HttpContext http, [FromHeader(Name = "Idempotency-Key")] string? requestKey, IBillingLedger ledger, CancellationToken ct) =>
            Write(http, requestKey, (actor, key) => ledger.CollectAsync(id, body, actor, key, ct))).WithName("BillingCollect").Produces<MoneyResult>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        collect.MapPost("/{paymentId:long}/confirm", (long id, long paymentId, TransferDecision body, HttpContext http, [FromHeader(Name = "Idempotency-Key")] string? requestKey, IBillingLedger ledger, CancellationToken ct) =>
            Write(http, requestKey, (actor, key) => ledger.DecideAsync(id, paymentId, body, true, actor, key, ct))).WithName("BillingConfirm").Produces<MoneyResult>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        collect.MapPost("/{paymentId:long}/cancel", (long id, long paymentId, TransferDecision body, HttpContext http, [FromHeader(Name = "Idempotency-Key")] string? requestKey, IBillingLedger ledger, CancellationToken ct) =>
            Write(http, requestKey, (actor, key) => ledger.DecideAsync(id, paymentId, body, false, actor, key, ct))).WithName("BillingCancel").Produces<MoneyResult>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        billing.MapPost("/invoices/{id:long}/waivers", (long id, WaiveBalance body, HttpContext http, [FromHeader(Name = "Idempotency-Key")] string? requestKey, IBillingLedger ledger, CancellationToken ct) =>
            Write(http, requestKey, (actor, key) => ledger.WaiveAsync(id, body, actor, key, ct)))
            .RequireAuthorization(policy => policy.RequireClaim("permission", "billing.waivers.approve")).WithName("BillingWaive").Produces<MoneyResult>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        billing.MapPost("/orders/{orderId:long}/close", async (long orderId, CloseOrderRequest body, ICloseOrder service, CancellationToken ct) =>
        {
            var result = await service.CloseAsync(orderId, body.BusinessDayId, ct);
            return result.Error is { } error ? Problem(error, error.EndsWith(".not_found", StringComparison.Ordinal) ? 404 : 409) : Results.Ok(result);
        }).RequireAuthorization(p => p.RequireClaim("permission", "sales.orders.manage"))
            .WithName("BillingCloseOrder").Produces<MoneyResult>().ProducesProblem(409).ProducesProblem(404);
        return billing;
    }

    private static async Task<IResult> Write(HttpContext http, string? requestKey, Func<Guid, Guid, Task<MoneyResult>> action)
    {
        if (!Guid.TryParse(http.User.FindFirst("sub")?.Value, out var actor)) return Results.Unauthorized();
        if (!Guid.TryParse(requestKey, out var key) || key == Guid.Empty)
            return Problem("billing.idempotency.required", 400);
        var result = await action(actor, key);
        if (result.Error is not { } error) return Results.Ok(result);
        return Problem(error, error.EndsWith(".not_found", StringComparison.Ordinal) ? 404 : error == "billing.input.invalid" ? 400 : 409);
    }

    private static IResult Problem(string code, int status) => Results.Problem(statusCode: status, title: code,
        extensions: new Dictionary<string, object?> { ["code"] = code });
}
