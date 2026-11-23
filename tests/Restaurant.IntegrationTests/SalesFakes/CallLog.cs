namespace Restaurant.IntegrationTests.SalesFakes;

/// <summary>One shared list the fakes write to, so a test can assert the order of calls (ADR-01 lock order).</summary>
public sealed class CallLog
{
    private readonly List<string> entries = [];

    public IReadOnlyList<string> Entries => entries;

    public void Add(string entry) => entries.Add(entry);

    public void Clear() => entries.Clear();
}
