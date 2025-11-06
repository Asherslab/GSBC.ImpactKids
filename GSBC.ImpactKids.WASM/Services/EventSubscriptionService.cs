using System.Text.RegularExpressions;
using Grpc.Core;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Events;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.Shared.Contracts.Services;
using GSBC.ImpactKids.WASM.Extensions;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using MudBlazor;
using Metadata = Grpc.Core.Metadata;

namespace GSBC.ImpactKids.WASM.Services;

public class EventSubscriptionService(
    IServiceProvider services,
    IEventService    unauthenticatedEventService
)
{
    public           Guid           StreamId { get; } = Guid.NewGuid();
    private          Task?          _eventTask;
    private readonly List<Callback> _callbacks = [];

    private bool _streamConnected;

    public async Task InitializeStreamIfDisconnected()
    {
        CallOptions callOptions;
        await using (AsyncServiceScope scope = services.CreateAsyncScope())
        {
            IAccessTokenProvider accessTokenProvider = scope.ServiceProvider.GetRequiredService<IAccessTokenProvider>();
            AccessTokenResult    result              = await accessTokenProvider.RequestAccessToken();

            if (!result.TryGetToken(out AccessToken? token))
                return;

            Metadata metadata = new() { { "Authorization", $"Bearer {token.Value}" } };
            callOptions = new CallOptions(metadata);
        }

        if (_eventTask == null || _eventTask.IsCompleted)
        {
            _eventTask = Task.Run(async () =>
                {
                    // this should run as long as the app is running, it will get "cancelled" once the browser tab is closed
                    while (true) 
                    {
                        try
                        {
                            await foreach (
                                EventResponse eventResp in unauthenticatedEventService.Stream(new EventStreamRequest
                                    {
                                        StreamId = StreamId
                                    },
                                    callOptions
                                )
                            )
                            {
                                if (!_streamConnected) // we just reconnected
                                    _ = Task.Run(async () => await RebindAll());
                                _streamConnected = true;
                                if (eventResp.RoutingKey == null)
                                    continue; // DON'T RETURN YOU DUMMY. KILLS THE STREAM

                                // Console.WriteLine($"TESTING: {eventResp.RoutingKey}");
                                foreach (
                                    Callback callback in _callbacks
                                        .Where(callback => callback.TopicMatcher.IsMatch(eventResp.RoutingKey))
                                )
                                {
                                    callback.Debounce();
                                }
                            }
                        }
                        catch (Exception)
                        {
                            // ignored
                        }
                        finally
                        {
                            _streamConnected = false;
                        }

                        await Task.Delay(TimeSpan.FromSeconds(5));
                    }
                    // ReSharper disable once FunctionNeverReturns
                }
            );
        }
    }

    private bool IsStreaming() => _streamConnected;

    public IEnumerable<Callback> GetCallbacks()
    {
        return _callbacks.ToList();
    }

    private async Task RebindAll()
    {
        await Task.Delay(TimeSpan.FromSeconds(0.1));

        int count = 1;
        while (!IsStreaming())
        {
            if (count > 5)
                return;
            await Task.Delay(TimeSpan.FromSeconds(count));
            count++;
        }

        await using AsyncServiceScope scope        = services.CreateAsyncScope();
        IEventService                 eventService = scope.ServiceProvider.GetRequiredService<IEventService>();
        ISnackbar                     snackbar     = scope.ServiceProvider.GetRequiredService<ISnackbar>();

        foreach (Callback callback in _callbacks)
        {
            await Bind(callback, eventService, snackbar);
        }
    }

    private async Task Bind(Callback callback, IEventService eventService, ISnackbar snackbar)
    {
        BasicReadResponse<Guid> resp = await eventService.Bind(new EventBindRequest
        {
            StreamId = StreamId,
            Topic = callback.Topic
        });

        if (resp.HasErrorOrNull())
        {
            snackbar.AddErrorResponse(resp);
        }

        callback.Subscription = resp.Entity;
    }

    public async Task AddCallback(Callback callback)
    {
        await using AsyncServiceScope scope        = services.CreateAsyncScope();
        IEventService                 eventService = scope.ServiceProvider.GetRequiredService<IEventService>();
        ISnackbar                     snackbar     = scope.ServiceProvider.GetRequiredService<ISnackbar>();
        await Bind(callback, eventService, snackbar);
        _callbacks.Add(callback);
    }

    public void RemoveCallback(Guid subscriptionId)
    {
        ICollection<Callback> callbacks = _callbacks.Where(x => x.Subscription == subscriptionId).ToList();
        _callbacks.RemoveAll(x => x.Subscription == subscriptionId);
        foreach (Callback callback in callbacks)
        {
            callback.Dispose();
        }
    }

    public class Callback : IDisposable
    {
        public required string     Topic         { get; init; }
        public required Regex      TopicMatcher  { get; init; }
        public          Guid?      Subscription  { get; set; }
        public required Func<Task> CallOnEvent   { get; init; }

        private readonly System.Timers.Timer _debounceTimer;
        public Callback()
        {
            _debounceTimer = new System.Timers.Timer(TimeSpan.FromMilliseconds(50));
            _debounceTimer.AutoReset = false;
            _debounceTimer.Elapsed += (_, _) => CallOnEvent?.Invoke();
        }

        public void Debounce()
        {
            _debounceTimer.Stop();
            _debounceTimer.Interval = TimeSpan.FromMilliseconds(50).TotalMilliseconds;
            _debounceTimer.Start();
        }

        public void Dispose()
        {
            _debounceTimer.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}