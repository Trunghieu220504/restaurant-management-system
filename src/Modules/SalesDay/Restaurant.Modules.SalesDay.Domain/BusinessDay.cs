namespace Restaurant.Modules.SalesDay.Domain;

public sealed class BusinessDay
{
    public Guid Id { get; set; }
    public DateOnly BusinessDate { get; set; }
    public DateTimeOffset OpenedAt { get; set; }
    public Guid OpenedBy { get; set; }
    public long OpeningCash { get; set; }
    public string Status { get; set; } = "DANG_BAN";
    public int? ActiveSlot { get; set; } = 1;
    public Guid RequestKey { get; set; }
    public byte[] Version { get; set; } = [];
}
