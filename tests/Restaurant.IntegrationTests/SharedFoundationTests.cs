using Restaurant.Shared.Contracts;
using Xunit;

namespace Restaurant.IntegrationTests;

public sealed class SharedFoundationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-key")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void Invalid_idempotency_keys_are_rejected(string? input) =>
        Assert.False(IdempotencyKeys.TryParse(input, out _));

    [Fact]
    public void Valid_idempotency_keys_preserve_the_existing_guid_contract()
    {
        var key = Guid.NewGuid();
        Assert.True(IdempotencyKeys.TryParse(key.ToString(), out var parsed));
        Assert.Equal(key, parsed);
    }

    [Theory]
    [InlineData("")]
    [InlineData("SalesDay.invalid")]
    [InlineData("salesday..invalid")]
    [InlineData("salesday invalid")]
    public void Error_codes_must_be_stable_lowercase_segments(string code) =>
        Assert.Throws<ArgumentException>(() => new BusinessError(code, "Rejected"));

    [Fact]
    public void Existing_error_codes_are_supported()
    {
        Assert.Equal("salesday.previous_not_closed", new BusinessError("salesday.previous_not_closed", "Day open").Code);
    }

    [Fact]
    public void Events_require_identity_positive_version_utc_and_payload()
    {
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        Assert.Throws<ArgumentException>(() => new IntegrationEvent<string>(Guid.Empty, now, "sales.order", 1, "payload"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new IntegrationEvent<string>(id, now, "sales.order", 0, "payload"));
        Assert.Throws<ArgumentException>(() => new IntegrationEvent<string>(id, now.ToOffset(TimeSpan.FromHours(7)), "sales.order", 1, "payload"));
        Assert.Throws<ArgumentNullException>(() => new IntegrationEvent<string>(id, now, "sales.order", 1, null!));
        var message = new IntegrationEvent<string>(id, now, "sales.order", 1, "payload");
        Assert.Equal(id, message.EventId);
    }
}
