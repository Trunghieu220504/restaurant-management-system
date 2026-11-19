namespace Restaurant.Shared.Contracts;

public static class IdempotencyKeys
{
    public static bool TryParse(string? value, out Guid key) =>
        Guid.TryParse(value, out key) && key != Guid.Empty;
}
