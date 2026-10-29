using Restaurant.Api.Configuration;
using Restaurant.Modules.Identity.Infrastructure;
using Restaurant.Modules.Identity.Presentation;
using Restaurant.Modules.Seating.Infrastructure;
using Restaurant.Modules.Seating.Presentation;
using Restaurant.Modules.Catalog.Infrastructure;
using Restaurant.Modules.Catalog.Presentation;
using Restaurant.Modules.Sales.Infrastructure;
using Restaurant.Modules.Sales.Presentation;
using Restaurant.Modules.Fulfillment.Infrastructure;
using Restaurant.Modules.Fulfillment.Presentation;
using Restaurant.Modules.Billing.Infrastructure;
using Restaurant.Modules.Billing.Presentation;
using Restaurant.Modules.Inventory.Infrastructure;
using Restaurant.Modules.Inventory.Presentation;
using Restaurant.Modules.Workforce.Infrastructure;
using Restaurant.Modules.Workforce.Presentation;
using Restaurant.Modules.Audit.Infrastructure;
using Restaurant.Modules.Audit.Presentation;
using Restaurant.Modules.Reporting.Infrastructure;
using Restaurant.Modules.Reporting.Presentation;

namespace Restaurant.Api.Modules;

public static class ModuleRegistration
{
    public static IServiceCollection AddRestaurantModules(
        this IServiceCollection services)
    {
        services.AddOptions<DatabaseSettings>()
            .BindConfiguration("ConnectionStrings")
            .Validate(settings => !string.IsNullOrWhiteSpace(settings.Restaurant),
                "Configure ConnectionStrings:Restaurant.")
            .ValidateOnStart();

        services.AddIdentityModule();
        services.AddSeatingModule();
        services.AddCatalogModule();
        services.AddSalesModule();
        services.AddFulfillmentModule();
        services.AddBillingModule();
        services.AddInventoryModule();
        services.AddWorkforceModule();
        services.AddAuditModule();
        services.AddReportingModule();
        services.AddHealthChecks().AddCheck<SqlServerHealthCheck>("sqlserver", tags: ["ready"]);
        return services;
    }

    public static void MapRestaurantModules(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapIdentityEndpoints();
        endpoints.MapSeatingEndpoints();
        endpoints.MapCatalogEndpoints();
        endpoints.MapSalesEndpoints();
        endpoints.MapFulfillmentEndpoints();
        endpoints.MapBillingEndpoints();
        endpoints.MapInventoryEndpoints();
        endpoints.MapWorkforceEndpoints();
        endpoints.MapAuditEndpoints();
        endpoints.MapReportingEndpoints();
    }

    public static IReadOnlyList<string> Names { get; } = Array.AsReadOnly<string>(
        ["Identity", "Seating", "Catalog", "Sales", "Fulfillment", "Billing", "Inventory", "Workforce", "Audit", "Reporting"]);
}
