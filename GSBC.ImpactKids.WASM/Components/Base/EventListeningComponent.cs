using System.Text.RegularExpressions;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Events;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.Shared.Contracts.Services;
using GSBC.ImpactKids.WASM.Extensions;
using GSBC.ImpactKids.WASM.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace GSBC.ImpactKids.WASM.Components.Base;

public abstract class EventListeningComponent : ComponentBase, IAsyncDisposable
{
    [Inject]
    public required EventSubscriptionService EventSubscriptionService { get; set; }

    [Inject]
    public required IEventService EventService { get; set; }

    [Inject]
    public required ISnackbar Snackbar { get; set; }

    [Inject]
    public required IDialogService DialogService { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        await EventSubscriptionService.InitializeStreamIfDisconnected();
        await Task.Delay(TimeSpan.FromSeconds(0.1)); // wait for the stream to have started before allowing subscriptions
    }

    protected async Task SubscribeToEvent(string topic, Func<Task> callOnEvent)
    {
        string regexMatch = topic.Replace("*", "([^.]+)").Replace("#", "([^.]+.?)+");
        regexMatch = $"^{regexMatch}$";
        Regex topicMatcher = new(regexMatch);

        await EventSubscriptionService.AddCallback(
            new EventSubscriptionService.Callback
            {
                Topic = topic,
                TopicMatcher = topicMatcher,
                CallOnEvent = callOnEvent
            }
        );
    }

    protected async Task Unbind()
    {
        foreach (EventSubscriptionService.Callback callback in EventSubscriptionService.GetCallbacks().Where(x => x.Subscription != null))
        {
            BasicResponse resp = await EventService.Unbind(new EventUnbindRequest
                {
                    StreamId = EventSubscriptionService.StreamId,
                    SubscriptionId = callback.Subscription!.Value
                }
            );
            EventSubscriptionService.RemoveCallback(callback.Subscription.Value);

            if (!resp.HasErrorOrNull()) continue;

            Snackbar.AddErrorResponse(resp);
            return;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await Unbind();
        GC.SuppressFinalize(this);
    }
}