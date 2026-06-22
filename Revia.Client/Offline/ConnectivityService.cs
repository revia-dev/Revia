using Microsoft.JSInterop;

namespace Revia.Client.Offline;

/// <summary>
/// Tracks online/offline state via the browser and raises an event on change.
/// The sync engine subscribes to flush the outbox when connectivity returns; the
/// UI can subscribe to show an "offline" indicator.
/// </summary>
public sealed class ConnectivityService(OfflineStore store) : IConnectivity, IAsyncDisposable
{
    private readonly OfflineStore _store = store;
    private DotNetObjectReference<ConnectivityService>? _ref;

    public bool IsOnline { get; private set; } = true;

    /// <summary>Raised when connectivity changes; argument is the new online state.</summary>
    public event Action<bool>? Changed;

    public async Task InitializeAsync()
    {
        IsOnline = await _store.IsOnlineAsync();
        _ref = DotNetObjectReference.Create(this);
        await _store.RegisterConnectivityAsync(_ref);
    }

    [JSInvokable]
    public Task OnConnectivityChanged(bool online)
    {
        if (online == IsOnline)
        {
            return Task.CompletedTask;
        }
        IsOnline = online;
        Changed?.Invoke(online);
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _ref?.Dispose();
        return ValueTask.CompletedTask;
    }
}
