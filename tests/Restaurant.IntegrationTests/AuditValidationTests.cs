using Restaurant.Modules.Audit.Application;
using Restaurant.Modules.Audit.Contracts;
using Restaurant.Modules.Audit.Domain;
using Xunit;

namespace Restaurant.IntegrationTests;

public sealed class AuditValidationTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 19, 0, 0, 0, TimeSpan.Zero);
    private static readonly AuditEntry Valid = new(Guid.NewGuid(), Now, Guid.NewGuid(), "Sales",
        "sales.batch.confirmed", "Order", "order-1", Guid.NewGuid(), "Lý do nghiệp vụ");

    [Fact]
    public void Sink_rejects_invalid_identifiers_times_codes_and_unbounded_metadata()
    {
        (string Name, AuditEntry Entry)[] cases =
        [
            ("event id", Valid with { EventId = Guid.Empty }),
            ("actor id", Valid with { ActorId = Guid.Empty }),
            ("business day id", Valid with { BusinessDayId = Guid.Empty }),
            ("human actor required", Valid with { ActorId = null }),
            ("unknown source", Valid with { SourceModule = "Unknown" }),
            ("default timestamp", Valid with { OccurredAtUtc = default }),
            ("local offset", Valid with { OccurredAtUtc = Now.ToOffset(TimeSpan.FromHours(7)) }),
            ("blank action", Valid with { Action = " " }),
            ("trimmed identifier", Valid with { EntityId = " order-1" }),
            ("bounded action", Valid with { Action = new string('x', 101) }),
            ("bounded entity type", Valid with { EntityType = new string('x', 101) }),
            ("bounded entity id", Valid with { EntityId = new string('x', 101) }),
            ("bounded reason", Valid with { Reason = new string('x', 513) }),
            ("control characters", Valid with { Reason = "header\r\nbody" })
        ];
        foreach (var (name, entry) in cases)
            Assert.True(Record.Exception(() => Create(entry)) is ArgumentException, name);
    }

    [Fact]
    public void System_event_can_omit_actor_and_optional_reason_is_preserved_exactly()
    {
        var system = Create(Valid with { SourceModule = "System", ActorId = null, BusinessDayId = null, Reason = null });
        Assert.Null(system.ActorId);
        Assert.Null(system.BusinessDayId);
        Assert.Null(system.Reason);
        Assert.Equal(string.Empty, Create(Valid with { Reason = string.Empty }).Reason);
        Assert.Equal(512, Create(Valid with { Reason = new string('ệ', 512) }).Reason!.Length);
        Assert.Throws<ArgumentException>(() => NhatKy.Create(Valid.EventId, Now, default, Valid.ActorId,
            Valid.SourceModule, Valid.Action, Valid.EntityType, Valid.EntityId, null, null));
    }

    [Fact]
    public void Query_bounds_reject_overflow_invalid_utc_ranges_and_empty_filters()
    {
        AuditLogQuery[] invalid =
        [
            new(Page: 0), new(Page: -1), new(PageSize: 0), new(PageSize: 101), new(Page: int.MaxValue, PageSize: 100),
            new(ActorId: Guid.Empty), new(EntityType: ""), new(EntityId: " "), new(EntityType: "Order\n"),
            new(EntityId: new string('x', 101)), new(FromUtc: default(DateTimeOffset)),
            new(FromUtc: Now, ToUtc: Now), new(FromUtc: Now.AddTicks(1), ToUtc: Now),
            new(ToUtc: Now.ToOffset(TimeSpan.FromHours(7)))
        ];
        Assert.All(invalid, query => Assert.False(query.IsValid));
        Assert.True(new AuditLogQuery().IsValid);
        Assert.True(new AuditLogQuery(FromUtc: Now, ToUtc: Now.AddTicks(1), PageSize: 100).IsValid);
    }

    private static NhatKy Create(AuditEntry entry) => NhatKy.Create(entry.EventId, entry.OccurredAtUtc, Now,
        entry.ActorId, entry.SourceModule, entry.Action, entry.EntityType, entry.EntityId, entry.BusinessDayId, entry.Reason);
}
