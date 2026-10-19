using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace GSBC.ImpactKids.ProPresenterAgent.ProPresenter;

/// <summary>
/// ProPresenter's own network API (Preferences → Network), at whatever address and port the admin
/// page says. Unauthenticated - ProPresenter's API has no auth - so it is only ever reached on the
/// church network, by this agent.
/// </summary>
public sealed class ProPresenterApi(HttpClient http, AgentState state, ILogger<ProPresenterApi> logger)
{
    private Uri? BaseUri => state.Configuration is { } configuration
        ? new Uri($"http://{configuration.ProPresenterHost}:{configuration.ProPresenterPort}/")
        : null;

    /// <summary>"ProPresenter 22", or null if it does not answer - closed, network off, or the wrong port.</summary>
    public async Task<string?> GetVersionAsync(CancellationToken token)
    {
        if (BaseUri is not { } baseUri)
            return null;

        try
        {
            VersionResponse? version = await http.GetFromJsonAsync<VersionResponse>(new Uri(baseUri, "version"), token);

            return version?.HostDescription ?? "ProPresenter";
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !token.IsCancellationRequested)
        {
            logger.LogDebug(exception, "ProPresenter at {Uri} did not answer", baseUri);
            return null;
        }
    }

    /// <summary>
    /// Makes ProPresenter repaint its stage screens, which is the only thing that makes a text box
    /// linked to an RSS feed show what it last fetched.
    /// <para>
    /// Proven on ProPresenter 22 on 2026-10-19: it re-fetches a linked feed every 10 seconds but never
    /// redraws on its own, and re-applying each stage screen's <i>current</i> layout forces the redraw
    /// with no visible flicker. Change metadata in the feed (<c>lastBuildDate</c>, a new <c>guid</c>)
    /// did not help.
    /// </para>
    /// </summary>
    public async Task RedrawStageAsync(CancellationToken token)
    {
        if (BaseUri is not { } baseUri)
            return;

        try
        {
            List<StageScreen> screens = await http.GetFromJsonAsync<List<StageScreen>>(
                new Uri(baseUri, "v1/stage/screens"), token) ?? [];

            foreach (StageScreen screen in screens)
            {
                StageLayoutId? layout = await http.GetFromJsonAsync<StageLayoutId>(
                    new Uri(baseUri, $"v1/stage/screen/{screen.Uuid}/layout"), token);

                if (layout?.Uuid == null)
                    continue;

                using HttpResponseMessage response = await http.GetAsync(
                    new Uri(baseUri, $"v1/stage/screen/{screen.Uuid}/layout/{layout.Uuid}"), token);

                // Logged every time: a refused re-apply (4xx) does not throw, and "the label did not
                // change on stage" is otherwise impossible to tell apart from "no redraw was sent".
                logger.LogInformation("Redrew stage screen {Screen} (layout {Layout}): {Status}",
                    screen.Name ?? screen.Uuid, layout.Name ?? layout.Uuid, (int)response.StatusCode);
            }

            if (screens.Count == 0)
                logger.LogWarning("ProPresenter has no stage screens to redraw");
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !token.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Could not make ProPresenter redraw its stage screens");
        }
    }

    /// <summary>The timers set up in ProPresenter, for the admin page to offer. Null when ProPresenter does not answer.</summary>
    public async Task<List<TimerId>?> GetTimersAsync(CancellationToken token)
    {
        if (BaseUri is not { } baseUri)
            return null;

        try
        {
            List<TimerConfig> timers = await http.GetFromJsonAsync<List<TimerConfig>>(new Uri(baseUri, "v1/timers"), token) ?? [];
            return timers.Select(t => t.Id).ToList();
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !token.IsCancellationRequested)
        {
            logger.LogDebug(exception, "Could not list ProPresenter's timers");
            return null;
        }
    }

    /// <summary>
    /// Sets a timer counting down from <paramref name="seconds"/> and starts it, allowed to run on past
    /// zero into overtime - which on the night is normal, and is how the stage shows it.
    /// <para>
    /// The <c>PUT …/start</c> form with the whole config, <c>id</c> included: proven on ProPresenter
    /// 22 to set and start in one call, where <c>GET …/start</c> on a just-changed timer answered and
    /// did not start. See docs/work/2026-10-propresenter-agent.md.
    /// </para>
    /// </summary>
    public async Task<bool> StartCountdownAsync(TimerId timer, int seconds, CancellationToken token)
    {
        if (BaseUri is not { } baseUri)
            return false;

        try
        {
            using HttpResponseMessage response = await http.PutAsJsonAsync(
                new Uri(baseUri, $"v1/timer/{timer.Uuid}/start"),
                new TimerConfig(timer, AllowsOverrun: true, new Countdown(Math.Max(0, seconds))),
                token);

            return response.IsSuccessStatusCode;
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !token.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Could not start ProPresenter timer {Name}", timer.Name);
            return false;
        }
    }

    /// <summary>Stops a timer and puts it back to its start - nothing is on tonight, or the night is over.</summary>
    public async Task ResetAsync(TimerId timer, CancellationToken token)
    {
        if (BaseUri is not { } baseUri)
            return;

        try
        {
            using HttpResponseMessage stop  = await http.GetAsync(new Uri(baseUri, $"v1/timer/{timer.Uuid}/stop"), token);
            using HttpResponseMessage reset = await http.GetAsync(new Uri(baseUri, $"v1/timer/{timer.Uuid}/reset"), token);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !token.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Could not reset ProPresenter timer {Name}", timer.Name);
        }
    }

    /// <summary>A timer's identity as ProPresenter gives it - all three parts go back in its config.</summary>
    public sealed record TimerId(
        [property: JsonPropertyName("uuid")]  string Uuid,
        [property: JsonPropertyName("name")]  string Name,
        [property: JsonPropertyName("index")] int    Index);

    private sealed record Countdown([property: JsonPropertyName("duration")] int Duration);

    private sealed record TimerConfig(
        [property: JsonPropertyName("id")]             TimerId    Id,
        [property: JsonPropertyName("allows_overrun")] bool       AllowsOverrun,
        [property: JsonPropertyName("countdown")]      Countdown? Countdown);

    private sealed record VersionResponse([property: JsonPropertyName("host_description")] string? HostDescription);

    // /v1/stage/screens answers a flat id; /v1/stage/screen/{id}/layout the same shape for the layout.
    private sealed record StageScreen(
        [property: JsonPropertyName("uuid")] string  Uuid,
        [property: JsonPropertyName("name")] string? Name);

    private sealed record StageLayoutId(
        [property: JsonPropertyName("uuid")] string? Uuid,
        [property: JsonPropertyName("name")] string? Name);
}
