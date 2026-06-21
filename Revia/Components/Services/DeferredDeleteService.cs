using Revia.Modules.Lending.Services;

namespace Revia.Services;

/// <summary>
/// Implements "delete with Undo" without requiring every module to support a
/// restore/soft-delete path. The caller hides the record from its own list
/// immediately (optimistic), then hands the real deletion here. We show an Undo
/// toast for a short window; only after it elapses (and the user did not undo) is
/// the supplied <c>commit</c> callback actually run against the database.
///
/// If the user taps Undo, the commit never runs and the caller's
/// <c>restore</c> callback puts the record back into the list.
///
/// Scoped per circuit so a navigation away still flushes pending deletes.
/// </summary>
public sealed class DeferredDeleteService(ToastService toast) : IDisposable
{
    private readonly ToastService _toast = toast;
    private readonly List<PendingDelete> _pending = new();

    private sealed class PendingDelete
    {
        public required CancellationTokenSource Cts { get; init; }
        public required Func<Task> Commit { get; init; }
        public bool Done { get; set; }
    }

    /// <summary>How long the Undo window stays open.</summary>
    public int UndoWindowMs { get; init; } = 6000;

    /// <param name="message">Toast text, e.g. "Film gelöscht".</param>
    /// <param name="commit">Runs the real deletion after the Undo window.</param>
    /// <param name="restore">Puts the record back into the caller's list on Undo.</param>
    public void DeleteWithUndo(string message, Func<Task> commit, Func<Task> restore)
    {
        var cts = new CancellationTokenSource();
        var entry = new PendingDelete { Cts = cts, Commit = commit };
        _pending.Add(entry);

        _toast.ShowWithAction(
            message,
            "Rückgängig",
            onAction: async () =>
            {
                if (entry.Done)
                {
                    return;
                }
                entry.Done = true;
                cts.Cancel();
                _pending.Remove(entry);
                await restore();
            },
            durationMs: UndoWindowMs);

        _ = CommitAfterDelayAsync(entry, cts.Token);
    }

    private async Task CommitAfterDelayAsync(PendingDelete entry, CancellationToken token)
    {
        try
        {
            await Task.Delay(UndoWindowMs, token);
        }
        catch (TaskCanceledException)
        {
            return; // undone
        }

        if (entry.Done)
        {
            return;
        }

        entry.Done = true;
        _pending.Remove(entry);

        try
        {
            await entry.Commit();
        }
        catch
        {
            // Commit failures are swallowed here; the caller already removed the
            // row optimistically. Individual pages surface their own errors where
            // they need to.
        }
    }

    /// <summary>Flush any still-pending deletes immediately (e.g. on dispose), so a
    /// quick navigation away doesn't leave a record un-deleted.</summary>
    public void Dispose()
    {
        foreach (var entry in _pending.ToList())
        {
            if (entry.Done)
            {
                continue;
            }
            entry.Done = true;
            entry.Cts.Cancel();
            // Best-effort synchronous-ish flush.
            _ = entry.Commit();
        }
        _pending.Clear();
    }
}
