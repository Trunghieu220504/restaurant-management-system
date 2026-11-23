namespace Restaurant.Modules.SalesDay.Application;

public sealed record CloseBlocker(string Code, int Count);
public sealed record CloseChecklist(Guid DayId, string Status, IReadOnlyList<CloseBlocker> Blockers);
public sealed record ClosingResult(CloseChecklist? Checklist, string? Error = null);
public interface IDayClosing
{
    Task<ClosingResult> ChecklistAsync(Guid dayId, CancellationToken ct);
    Task<ClosingResult> ChangeAsync(Guid dayId, bool cancel, Guid actor, Guid key, CancellationToken ct);
}
