using System.Reflection;
using EasyAppDev.Blazor.Store.Core;
using EasyAppDev.Blazor.Store.Utilities;
using GSBC.ImpactKids.WASM.Services.RefreshableStore;
using Microsoft.JSInterop;
using Module = GSBC.ImpactKids.Shared.Contracts.Module;

namespace GSBC.ImpactKids.WASM.Features.Eventing.Services;

public sealed class SseClientService(
    IJSRuntime                js,
    IConfiguration            configuration,
    IStore<EventsStreamState> state,
    ILazyCache                lazyCache,
    IServiceProvider          services
) : ISseClientService
{
    private IJSObjectReference?                      _module;
    private DotNetObjectReference<SseClientService>? _selfRef;

    public bool Connected { get; private set; }
    public bool Started   { get; private set; }

    // public string? LastEventId { get; private set; }

    private CancellationTokenSource _tokenSource = new();

    public async Task StartAsync()
    {
        Started = true;

        if (!_tokenSource.IsCancellationRequested)
            await _tokenSource.CancelAsync();

        _tokenSource = new CancellationTokenSource();
        CancellationToken token = _tokenSource.Token;

        _ = Task.Run(async () =>
            {
                try
                {
                    _module ??= await js.InvokeAsync<IJSObjectReference>("import", token, "./js/sseEventSource.js");
                    _selfRef ??= DotNetObjectReference.Create(this);
                    await _module.InvokeVoidAsync(
                        "start",
                        token,
                        configuration["Services:yarp:https:0"] + "/api/stream",
                        _selfRef
                    );
                }
                finally
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), token);
                    Started = false;
                }
            },
            token
        );
    }

    public async Task StopAsync()
    {
        if (_module is not null)
            await _module.InvokeVoidAsync("stop");
    }

    // JS -> .NET callbacks
    [JSInvokable]
    public async Task OnOpen()
    {
        Connected = true;
        await state.UpdateAsync(s => s.SetConnected(true));
    }

    [JSInvokable]
    public async Task OnMessage(string data, string? id, string? eventType)
    {
        Type? entityType = Assembly.GetAssembly(typeof(Module))?.GetType(data);
        if (entityType != null)
        {
            MethodInfo? method  = typeof(SseClientService).GetMethod(nameof(Refresh));
            MethodInfo? generic = method?.MakeGenericMethod(entityType);

            try
            {
                if (generic != null)
                {
                    object? obj = generic.Invoke(this, null);
                    if (obj is Task task)
                        await task;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }
    }

    /// <summary>
    /// How long a burst of events for one entity type is allowed to settle before the store
    /// is refreshed. Trailing, never leading - see <see cref="Refresh{T}" />.
    /// </summary>
    private static readonly TimeSpan RefreshDebounce = TimeSpan.FromMilliseconds(300);

    private readonly Dictionary<string, CancellationTokenSource> _pendingRefreshes = new();
    private readonly object                                      _pendingGate      = new();

    /// <summary>
    /// Refreshes the store for <typeparamref name="T" />, after a short quiet period.
    /// <para>
    /// <b>The debounce is what stops a screen missing an update.</b> Every write raises its
    /// own event, so signing a household out fires several within a second. Each one used to
    /// start its own refresh, and those refreshes overlap: the read for the first write can
    /// still be in flight when the read for the last one comes back, and whichever finishes
    /// last is the one that writes the store. When the slow, early read lands last it puts
    /// the pre-write list back, and the row it covers shows the old state until something
    /// else happens to refresh it. Measured: two writes, two reads, one row correct and one
    /// stale, with both rows already correct in the database.
    /// </para>
    /// <para>
    /// Collapsing the burst into one refresh that starts after the last event removes the
    /// overlap rather than racing it. It must be <b>trailing</b> - firing on the first event
    /// and ignoring the rest reads the data before the later writes land, which is the same
    /// bug wearing a different hat.
    /// </para>
    /// </summary>
    public async Task Refresh<T>()
    {
        string key = typeof(T).Name;

        CancellationTokenSource cts = new();

        lock (_pendingGate)
        {
            if (_pendingRefreshes.Remove(key, out CancellationTokenSource? superseded))
            {
                superseded.Cancel();
                superseded.Dispose();
            }

            _pendingRefreshes[key] = cts;
        }

        try
        {
            await Task.Delay(RefreshDebounce, cts.Token);
        }
        catch (OperationCanceledException)
        {
            // A newer event for this type arrived, and it owns the refresh now.
            return;
        }
        finally
        {
            lock (_pendingGate)
            {
                if (_pendingRefreshes.TryGetValue(key, out CancellationTokenSource? current) && current == cts)
                    _pendingRefreshes.Remove(key);
            }

            cts.Dispose();
        }

        IRefreshableStore<T>? refreshableService = services.GetService<IRefreshableStore<T>>();

        await lazyCache.RemoveAsync($"{key}-list");
        if (refreshableService != null)
            await refreshableService.RefreshEvent();
    }

    [JSInvokable]
    public async Task OnError(string message)
    {
        Connected = false;
        await state.UpdateAsync(s => s.SetConnected(false));
    }

    [JSInvokable]
    public async Task OnStopped()
    {
        Connected = false;
        await state.UpdateAsync(s => s.SetConnected(false));
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        if (_module is not null)
        {
            try
            {
                await _module.DisposeAsync();
            }
            catch
            {
                // ignored
            }

            _module = null;
        }

        _selfRef?.Dispose();
        _selfRef = null;
    }
}