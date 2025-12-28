using System.Reflection;
using System.Threading.Channels;
using EasyAppDev.Blazor.Store.Core;
using EasyAppDev.Blazor.Store.Utilities;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.Shared.Contracts.Services.Features.Eventing;
using GSBC.ImpactKids.WASM.Extensions;
using GSBC.ImpactKids.WASM.Services.RefreshableStore;
using Microsoft.AspNetCore.Components.Authorization;
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

    // public string? LastEventId { get; private set; }

    private CancellationTokenSource _tokenSource = new();

    public async Task StartAsync()
    {
        if (!_tokenSource.IsCancellationRequested)
            await _tokenSource.CancelAsync();

        _tokenSource = new CancellationTokenSource();
        CancellationToken token = _tokenSource.Token;

        _ = Task.Run(async () =>
            {
                AuthenticationState authState;
                do
                {
                    using IServiceScope authScope = services.CreateScope();

                    AuthenticationStateProvider authStateProvider =
                        authScope.ServiceProvider.GetRequiredService<AuthenticationStateProvider>();
                    authState = await authStateProvider.GetAuthenticationStateAsync();

                    if (authState.User.Identity?.IsAuthenticated != true)
                        await Task.Delay(TimeSpan.FromSeconds(1), token);
                } while (authState.User.Identity?.IsAuthenticated != true && !token.IsCancellationRequested);

                if (token.IsCancellationRequested)
                    return;

                BasicReadResponse<Guid> resp;
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
                    configuration["Services:grpc:https:0"] + "/stream?StreamId=" + resp.Entity,
                    _selfRef
                );
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
            Type finishedServiceType = typeof(IRefreshableStore<>).MakeGenericType(entityType);

            try
            {
                object? refreshableService = services.GetService(finishedServiceType);

                if (refreshableService is IRefreshableStore refreshableStore)
                {
                    Console.WriteLine(data + "   " + $"{data.Split(".").LastOrDefault()}-list");
                    await lazyCache.RemoveAsync($"{data.Split(".").LastOrDefault()}-list");
                    await refreshableStore.RefreshEvent();
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

    [JSInvokable]
    public async Task OnError(string message)
    {
        await state.UpdateAsync(s => s.SetConnected(false));
    }

    [JSInvokable]
    public async Task OnStopped()
    {
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