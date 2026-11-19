namespace Restaurant.Modules.Identity.Application.Authorization;

public static class PermissionCodes
{
    public const string IdentityAccountsManage = "identity.accounts.manage";
    public const string IdentityRolesManage = "identity.roles.manage";
    public const string IdentitySubordinatesManage = "identity.subordinates.manage";
    public const string SeatingCatalogManage = "seating.catalog.manage";
    public const string SeatingSessionsManage = "seating.sessions.manage";
    public const string CatalogMenuManage = "catalog.menu.manage";
    public const string CatalogAvailabilityManage = "catalog.availability.manage";
    public const string SalesOrdersManage = "sales.orders.manage";
    public const string SalesBatchesConfirm = "sales.batches.confirm";
    public const string FulfillmentKitchenProcess = "fulfillment.kitchen.process";
    public const string FulfillmentBarProcess = "fulfillment.bar.process";
    public const string FulfillmentIncidentsReport = "fulfillment.incidents.report";
    public const string FulfillmentIncidentsApprove = "fulfillment.incidents.approve";
    public const string FulfillmentPortionsServe = "fulfillment.portions.serve";
    public const string BillingPaymentsCollect = "billing.payments.collect";
    public const string BillingRefundsRequest = "billing.refunds.request";
    public const string BillingRefundsApprove = "billing.refunds.approve";
    public const string BillingRefundsPay = "billing.refunds.pay";
    public const string BillingWaiversApprove = "billing.waivers.approve";
    public const string InventoryCatalogManage = "inventory.catalog.manage";
    public const string InventoryPurchasesManage = "inventory.purchases.manage";
    public const string InventoryCountsEnter = "inventory.counts.enter";
    public const string InventoryCountsApprove = "inventory.counts.approve";
    public const string InventoryAdjustmentsApprove = "inventory.adjustments.approve";
    public const string SalesdayManage = "salesday.manage";
    public const string ReportingRead = "reporting.read";

    public static IReadOnlyList<string> All { get; } = Array.AsReadOnly<string>(
    [
        IdentityAccountsManage,
        IdentityRolesManage,
        IdentitySubordinatesManage,
        SeatingCatalogManage,
        SeatingSessionsManage,
        CatalogMenuManage,
        CatalogAvailabilityManage,
        SalesOrdersManage,
        SalesBatchesConfirm,
        FulfillmentKitchenProcess,
        FulfillmentBarProcess,
        FulfillmentIncidentsReport,
        FulfillmentIncidentsApprove,
        FulfillmentPortionsServe,
        BillingPaymentsCollect,
        BillingRefundsRequest,
        BillingRefundsApprove,
        BillingRefundsPay,
        BillingWaiversApprove,
        InventoryCatalogManage,
        InventoryPurchasesManage,
        InventoryCountsEnter,
        InventoryCountsApprove,
        InventoryAdjustmentsApprove,
        SalesdayManage,
        ReportingRead,
    ]);
}
