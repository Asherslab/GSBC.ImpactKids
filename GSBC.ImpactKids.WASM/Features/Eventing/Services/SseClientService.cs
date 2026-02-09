using System.Reflection;
using System.Threading.Channels;
using EasyAppDev.Blazor.Store.Core;
using EasyAppDev.Blazor.Store.Utilities;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.Shared.Contracts.Services.Features.Eventing;
using GSBC.ImpactKids.WASM.Extensions;
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

    private readonly Channel<SseMessage> _channel = Channel.CreateBounded<SseMessage>(
        new BoundedChannelOptions(capacity: 1024)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = false,
            SingleWriter = true
        });

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
                    BasicReadResponse<Guid>? resp;
                    do
                    {
                        using IServiceScope serviceScope = services.CreateScope();

                        IEventingService eventingService =
                            serviceScope.ServiceProvider.GetRequiredService<IEventingService>();
                        resp = await eventingService.GetStreamId();

                        if (resp.HasErrorOrNull())
                            await Task.Delay(TimeSpan.FromSeconds(1), token);
                    } while (resp.HasErrorOrNull() && !token.IsCancellationRequested);

                    if (token.IsCancellationRequested)
                        return;

                    _module ??= await js.InvokeAsync<IJSObjectReference>("import", token, "./js/sseEventSource.js");
                    _selfRef ??= DotNetObjectReference.Create(this);
                    await _module.InvokeVoidAsync(
                        "start",
                        token,
                        configuration["Services:yarp:https:0"] + "/api/stream?StreamId=" +
                        resp.Entity,
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
        // LastEventId = string.IsNullOrWhiteSpace(id) ? LastEventId : id;

        // using IServiceScope scope = services.CreateScope();

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

        _channel.Writer.TryWrite(new SseMessage(data, id, eventType));
    }

    public async Task Refresh<T>()
    {
        IRefreshableStore<T>? refreshableService = services.GetService<IRefreshableStore<T>>();

        await lazyCache.RemoveAsync($"{typeof(T).Name}-list");
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

    public async IAsyncEnumerable<SseMessage> GetMessagesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken ct = default
    )
    {
        while (await _channel.Reader.WaitToReadAsync(ct))
        {
            while (_channel.Reader.TryRead(out SseMessage? msg))
            {
                yield return msg;
            }
        }
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
        _channel.Writer.TryComplete();
    }
}