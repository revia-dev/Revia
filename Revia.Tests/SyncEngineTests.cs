using Revia.Client.Offline;
using Revia.Shared.Offline;

namespace Revia.Tests;

/// <summary>
/// Tests the offline SyncEngine's replay logic against in-memory fakes — the riskiest
/// part of the offline-first design. Covers: FIFO replay, server-wins dispatch, that a
/// failing mutation backs off and is eventually poisoned without wedging the queue,
/// dropping mutations for unknown collections, and not flushing while offline.
/// </summary>
public class SyncEngineTests
{
    // ---- Fakes ----

    private sealed class FakeConnectivity(bool online = true) : IConnectivity
    {
        public bool IsOnline { get; set; } = online;
        public event Action<bool>? Changed;
        public void Go(bool online) { IsOnline = online; Changed?.Invoke(online); }
    }

    private sealed class FakeOutboxStore : IOutboxStore
    {
        // Preserve insertion order so FIFO assertions are meaningful.
        public readonly List<OutboxMutation> Items = new();

        public Task<List<OutboxMutation>> OutboxAllAsync() => Task.FromResult(Items.ToList());

        public Task OutboxUpdateAsync(OutboxMutation mutation)
        {
            var i = Items.FindIndex(m => m.Id == mutation.Id);
            if (i >= 0) { Items[i] = mutation; }
            return Task.CompletedTask;
        }

        public Task OutboxRemoveAsync(string id)
        {
            Items.RemoveAll(m => m.Id == id);
            return Task.CompletedTask;
        }

        public Task<int> OutboxCountAsync() => Task.FromResult(Items.Count);
    }

    private static OutboxMutation Mut(string collection, string id) =>
        new() { Id = id, Collection = collection, Kind = MutationKind.Create };

    // ---- Tests ----

    [Fact]
    public async Task Flush_ReplaysMutations_OldestFirst_AndRemovesOnSuccess()
    {
        var store = new FakeOutboxStore();
        store.Items.Add(Mut("wishlists", "1"));
        store.Items.Add(Mut("wishlists", "2"));
        store.Items.Add(Mut("wishlists", "3"));

        var replayed = new List<string>();
        var engine = new SyncEngine(store, new FakeConnectivity());
        // Mirror the real contract: a handler removes the mutation from the outbox
        // on success (see OfflineRepository.TryReplayAsync).
        engine.RegisterHandler("wishlists", async m =>
        {
            replayed.Add(m.Id);
            await store.OutboxRemoveAsync(m.Id);
            return true;
        });

        var result = await engine.FlushAsync();

        Assert.Equal(new[] { "1", "2", "3" }, replayed); // FIFO
        Assert.Equal(3, result.Sent);
        Assert.Equal(0, result.Remaining);
        Assert.Empty(store.Items);
    }

    [Fact]
    public async Task Flush_WhenOffline_DoesNotReplay()
    {
        var store = new FakeOutboxStore();
        store.Items.Add(Mut("wishlists", "1"));

        var replayed = 0;
        var engine = new SyncEngine(store, new FakeConnectivity(online: false));
        engine.RegisterHandler("wishlists", _ => { replayed++; return Task.FromResult(true); });

        var result = await engine.FlushAsync();

        Assert.Equal(0, replayed);
        Assert.Equal(1, result.Remaining);
    }

    [Fact]
    public async Task Flush_UnknownCollection_IsDropped()
    {
        var store = new FakeOutboxStore();
        store.Items.Add(Mut("removed-module", "1"));

        var engine = new SyncEngine(store, new FakeConnectivity());
        // No handler registered for "removed-module".

        var result = await engine.FlushAsync();

        Assert.Empty(store.Items);          // dropped so it can't wedge the queue
        Assert.Equal(0, result.Remaining);
    }

    [Fact]
    public async Task Flush_TransientFailure_BacksOff_ThenSkipsUntilDue()
    {
        var store = new FakeOutboxStore();
        store.Items.Add(Mut("wishlists", "1"));

        var attempts = 0;
        var engine = new SyncEngine(store, new FakeConnectivity());
        engine.RegisterHandler("wishlists", _ => { attempts++; return Task.FromResult(false); });

        // First flush: one attempt, fails, gets a future NextAttemptAt.
        var first = await engine.FlushAsync();
        Assert.Equal(1, attempts);
        Assert.Equal(1, first.Failed);
        Assert.Equal(1, first.Remaining);
        var queued = store.Items.Single();
        Assert.Equal(1, queued.Attempts);
        Assert.NotNull(queued.NextAttemptAt);

        // Second flush immediately after: still within the backoff window → skipped.
        var second = await engine.FlushAsync();
        Assert.Equal(1, attempts); // unchanged — not retried yet
        Assert.Equal(1, second.Remaining);
    }

    [Fact]
    public async Task Flush_RepeatedFailures_EventuallyPoison_WithoutBlockingOthers()
    {
        var store = new FakeOutboxStore();
        var bad = Mut("wishlists", "bad");
        store.Items.Add(bad);
        store.Items.Add(Mut("wishlists", "good"));

        var goodSent = false;
        var engine = new SyncEngine(store, new FakeConnectivity());
        engine.RegisterHandler("wishlists", async m =>
        {
            if (m.Id == "bad") { return false; }
            goodSent = true;
            await store.OutboxRemoveAsync(m.Id);
            return true;
        });

        // Drive enough flushes to exceed MaxAttempts (6); clear the backoff between
        // flushes so each pass actually retries the bad one.
        for (var i = 0; i < 6; i++)
        {
            foreach (var m in store.Items) { m.NextAttemptAt = null; }
            await engine.FlushAsync();
        }

        var poison = await engine.PoisonCountAsync();
        Assert.Equal(1, poison);                       // bad one is poisoned
        Assert.True(goodSent);                         // good one still went through
        Assert.DoesNotContain(store.Items, m => m.Id == "good"); // good removed
        Assert.Contains(store.Items, m => m.Id == "bad" && m.Poison);
    }

    [Fact]
    public async Task RetryPoisoned_ClearsFlags_AndRetries()
    {
        var store = new FakeOutboxStore();
        store.Items.Add(new OutboxMutation
        {
            Id = "1", Collection = "wishlists", Kind = MutationKind.Create,
            Poison = true, Attempts = 6
        });

        var attempts = 0;
        var engine = new SyncEngine(store, new FakeConnectivity());
        engine.RegisterHandler("wishlists", async m =>
        {
            attempts++;
            await store.OutboxRemoveAsync(m.Id);
            return true;
        });

        await engine.RetryPoisonedAsync();

        Assert.Equal(1, attempts);     // the cleared mutation was retried
        Assert.Empty(store.Items);     // and succeeded
    }

    [Fact]
    public async Task Flush_HandlerThrows_IsTreatedAsTransientFailure()
    {
        var store = new FakeOutboxStore();
        store.Items.Add(Mut("wishlists", "1"));

        var engine = new SyncEngine(store, new FakeConnectivity());
        engine.RegisterHandler("wishlists", _ => throw new InvalidOperationException("boom"));

        var result = await engine.FlushAsync();

        Assert.Equal(1, result.Failed);
        Assert.Equal(1, result.Remaining);
        Assert.Equal("boom", store.Items.Single().LastError);
    }
}
