namespace Restaurant.Modules.SalesDay.Domain;

public sealed class ClosingEntry
{
    public Guid DayId { get; set; }
    public Guid RequestKey { get; set; }
    public Guid Actor { get; set; }
    public string Status { get; set; } = "";
    public DateTimeOffset ChangedAt { get; set; }
}
