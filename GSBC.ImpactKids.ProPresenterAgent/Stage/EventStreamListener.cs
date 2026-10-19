using System.Net.Http.Headers;
using System.Net.ServerSentEvents;
using GSBC.ImpactKids.ProPresenterAgent.ImpactKids;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.ProPresenter;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.ProPresenter;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.Extensions.Options;

namespace GSBC.ImpactKids.ProPresenterAgent.Stage;

/// <summary>
/// Listens on Impact Kids' change events - the same stream every open page listens on, at the
/// machine's own path (<c>machine/stream</c>) - so a press of Next on a phone reaches the stage in
/// about a second instead of at the next 30-second check-in.
/// <para>
/// An event says only which kind of thing changed. A schedule change re-reads tonight; a change to
/// the machines re-reads this machine's settings, since the timer to drive may have changed.
/// Everything else is ignored. A dropped connection is retried every few seconds, and tonight is
/// re-read on every reconnect in case something changed while it was down.
/// </para>
/// </summary>
public sealed class EventStreamListener(
    IHttpClientFactory           httpFactory,
    IOptions<AgentOptions>       options,
    ImpactKidsClient             impactKids,
    StageDriver                  stage,
    AgentState                   state,
    ILogger<EventStreamListener> logger
) : BackgroundService
{
    public const string HttpClientName = "events";

    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    private static readonly string ScheduleEvent = typeof(Shared.Contracts.Entities.Features.Scheduling.Schedules.Schedule).FullName!;
    private static readonly string MachineEvent  = typeof(ProPresenterMachine).FullName!;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (!state.Connected)
            {
                await Task.Delay(RetryDelay, stoppingToken);
                continue;
            }

            try
            {
                await ListenAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning("Event stream dropped: {Message}. Reconnecting", exception.Message);
            }

            await Task.Delay(RetryDelay, stoppingToken);
        }
    }

    private async Task ListenAsync(CancellationToken token)
    {
        HttpClient http = httpFactory.CreateClient(HttpClientName);

        using HttpRequestMessage request = new(HttpMethod.Get, new Uri(options.Value.ImpactKidsUrl, "machine/stream"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await impactKids.AccessTokenAsync(token));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        using HttpResponseMessage response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();

        logger.LogInformation("Listening for schedule changes");

        // Whatever happened while the stream was down.
        await stage.RefreshAsync(token);

        await using Stream stream = await response.Content.ReadAsStreamAsync(token);

        await foreach (SseItem<string> item in SseParser.Create(stream).EnumerateAsync(token))
        {
            if (item.EventType != "message")
                continue;

            if (item.Data == ScheduleEvent)
                await stage.RefreshAsync(token);
            else if (item.Data == MachineEvent)
                await RefreshConfigurationAsync(token);
        }
    }

    private async Task RefreshConfigurationAsync(CancellationToken token)
    {
        BasicReadResponse<ProPresenterAgentConfiguration> configuration =
            await impactKids.Agent.GetConfiguration(new ProPresenterAgentRequest(), token);

        if (configuration is { Success: true, Entity: not null } && configuration.Entity != state.Configuration)
        {
            state.Configuration = configuration.Entity;
            await stage.ApplyAsync(token);
        }
    }
}
