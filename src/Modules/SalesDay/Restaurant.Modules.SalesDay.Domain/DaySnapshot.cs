namespace Restaurant.Modules.SalesDay.Domain;

public sealed class DaySnapshot
{
    public Guid DayId { get; set; }
    public int SnapshotVersion { get; set; } = 1;
    public DateOnly BusinessDate { get; set; }
    public Guid ClosedBy { get; set; }
    public DateTimeOffset ClosedAt { get; set; }
    public Guid RequestKey { get; set; }
    public bool LateClose { get; set; }
    public string? LateReason { get; set; }
    public decimal OpeningCash { get; set; }
    public decimal ExpectedCash { get; set; }
    public decimal CountedCash { get; set; }
    public decimal CashDifference => CountedCash - ExpectedCash;
    public string? DifferenceReason { get; set; }
    public decimal DrawerPurchases { get; set; }
    public decimal Receipts { get; set; }
    public decimal Refunds { get; set; }
    public decimal Charges { get; set; }
    public decimal Adjustments { get; set; }
    public decimal Waivers { get; set; }
    public decimal NetRevenue { get; set; }
    public decimal Cost { get; set; }
    public decimal RoundingDifference { get; set; }
    public decimal GrossProfit { get; set; }
    public List<DayMethodSnapshot> Methods { get; set; } = [];
}
public sealed class DayMethodSnapshot
{
    public Guid DayId { get; set; }
    public string Method { get; set; } = "";
    public decimal Receipts { get; set; }
    public decimal Refunds { get; set; }
    public decimal Net { get; set; }
}
