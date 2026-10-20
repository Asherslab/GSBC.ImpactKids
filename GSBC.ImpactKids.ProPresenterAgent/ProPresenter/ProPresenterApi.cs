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

    /// <summary>ProPresenter's libraries, for the admin page to offer and the song sync to read. Null when ProPresenter does not answer.</summary>
    public async Task<List<LibraryId>?> GetLibrariesAsync(CancellationToken token)
    {
        if (BaseUri is not { } baseUri)
            return null;

        try
        {
            return await http.GetFromJsonAsync<List<LibraryId>>(new Uri(baseUri, "v1/libraries"), token) ?? [];
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !token.IsCancellationRequested)
        {
            logger.LogDebug(exception, "Could not list ProPresenter's libraries");
            return null;
        }
    }

    /// <summary>
    /// The presentations in one library. Their names here can carry the arrangement -
    /// "Father Abraham - [ Default ]" - so take a song's name from <see cref="GetPresentationAsync"/>.
    /// Null when ProPresenter does not answer.
    /// </summary>
    public async Task<List<LibraryItem>?> GetLibraryItemsAsync(string libraryUuid, CancellationToken token)
    {
        if (BaseUri is not { } baseUri)
            return null;

        try
        {
            LibraryItems? library = await http.GetFromJsonAsync<LibraryItems>(new Uri(baseUri, $"v1/library/{libraryUuid}"), token);
            return library?.Items ?? [];
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !token.IsCancellationRequested)
        {
            logger.LogWarning("Could not read ProPresenter library {Library}: {Message}", libraryUuid, exception.Message);
            return null;
        }
    }

    /// <summary>One presentation, with every group's slide text and its arrangements. Null when ProPresenter does not answer.</summary>
    public async Task<Presentation?> GetPresentationAsync(string uuid, CancellationToken token)
    {
        if (BaseUri is not { } baseUri)
            return null;

        try
        {
            PresentationResponse? response = await http.GetFromJsonAsync<PresentationResponse>(new Uri(baseUri, $"v1/presentation/{uuid}"), token);
            return response?.Presentation;
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !token.IsCancellationRequested)
        {
            logger.LogWarning("Could not read ProPresenter presentation {Uuid}: {Message}", uuid, exception.Message);
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

    /// <summary>
    /// Sets a timer counting down to a time of day on this Mac's clock, and starts it - ProPresenter's
    /// own "countdown to time" type, so a wait of hours shows as a time rather than as thousands of
    /// minutes. ProPresenter takes the time as seconds on a 12-hour clock plus am/pm (its "PreShow
    /// Countdown" reads <c>time_of_day: 18000, period: "pm"</c> for 5:00 pm).
    /// </summary>
    public async Task<bool> StartCountdownToTimeAsync(TimerId timer, TimeSpan timeOfDay, CancellationToken token)
    {
        if (BaseUri is not { } baseUri)
            return false;

        int seconds = (int)timeOfDay.TotalSeconds % (24 * 3600);
        string period = seconds >= 12 * 3600 ? "pm" : "am";

        try
        {
            using HttpResponseMessage response = await http.PutAsJsonAsync(
                new Uri(baseUri, $"v1/timer/{timer.Uuid}/start"),
                new TimerToTimeConfig(timer, AllowsOverrun: true, new CountdownToTime(seconds % (12 * 3600), period)),
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

    private sealed record CountdownToTime(
        [property: JsonPropertyName("time_of_day")] int    TimeOfDay,
        [property: JsonPropertyName("period")]      string Period);

    private sealed record TimerToTimeConfig(
        [property: JsonPropertyName("id")]                 TimerId         Id,
        [property: JsonPropertyName("allows_overrun")]     bool            AllowsOverrun,
        [property: JsonPropertyName("count_down_to_time")] CountdownToTime CountDownToTime);

    private sealed record TimerConfig(
        [property: JsonPropertyName("id")]             TimerId    Id,
        [property: JsonPropertyName("allows_overrun")] bool       AllowsOverrun,
        [property: JsonPropertyName("countdown")]      Countdown? Countdown);

    private sealed record VersionResponse([property: JsonPropertyName("host_description")] string? HostDescription);

    // /v1/libraries answers a flat list of these; /v1/library/{id} wraps its items in "items".
    public sealed record LibraryId(
        [property: JsonPropertyName("uuid")] string Uuid,
        [property: JsonPropertyName("name")] string Name);

    public sealed record LibraryItem(
        [property: JsonPropertyName("uuid")] string Uuid,
        [property: JsonPropertyName("name")] string Name);

    private sealed record LibraryItems([property: JsonPropertyName("items")] List<LibraryItem>? Items);

    private sealed record PresentationResponse([property: JsonPropertyName("presentation")] Presentation? Presentation);

    /// <summary>
    /// <c>/v1/presentation/{uuid}</c>, the parts the song library uses. Proven on ProPresenter 22 on
    /// 2026-10-20 against all 26 songs: the arrangement list always starts with "Master", whose uuid is
    /// the presentation's own and which ProPresenter makes up - it is not stored with the song.
    /// </summary>
    public sealed record Presentation(
        [property: JsonPropertyName("id")]                  LibraryItem               Id,
        [property: JsonPropertyName("groups")]              List<PresentationGroup>?  Groups,
        [property: JsonPropertyName("arrangements")]        List<Arrangement>?        Arrangements,
        [property: JsonPropertyName("current_arrangement")] string?                   CurrentArrangement);

    public sealed record PresentationGroup(
        [property: JsonPropertyName("uuid")]   string                    Uuid,
        [property: JsonPropertyName("name")]   string?                   Name,
        [property: JsonPropertyName("color")]  GroupColor?               Color,
        [property: JsonPropertyName("slides")] List<PresentationSlide>?  Slides);

    /// <summary>Each channel 0 to 1.</summary>
    public sealed record GroupColor(
        [property: JsonPropertyName("red")]   double Red,
        [property: JsonPropertyName("green")] double Green,
        [property: JsonPropertyName("blue")]  double Blue);

    public sealed record PresentationSlide([property: JsonPropertyName("text")] string? Text);

    public sealed record Arrangement(
        [property: JsonPropertyName("id")]     LibraryItem   Id,
        [property: JsonPropertyName("groups")] List<string>? Groups);

    // /v1/stage/screens answers a flat id; /v1/stage/screen/{id}/layout the same shape for the layout.
    private sealed record StageScreen(
        [property: JsonPropertyName("uuid")] string  Uuid,
        [property: JsonPropertyName("name")] string? Name);

    private sealed record StageLayoutId(
        [property: JsonPropertyName("uuid")] string? Uuid,
        [property: JsonPropertyName("name")] string? Name);
}
