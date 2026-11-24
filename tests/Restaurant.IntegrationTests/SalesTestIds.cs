namespace Restaurant.IntegrationTests;

/// <summary>Readable catalog ids for Sales tests: <c>CatalogId.Of(7)</c> is always the same Guid.</summary>
internal static class CatalogId
{
    public static Guid Of(int n) => new(n, 0, 0, new byte[8]);
}
