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
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !token.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Could not make ProPresenter redraw its stage screens");
        }
    }

    private sealed record VersionResponse([property: JsonPropertyName("host_description")] string? HostDescription);

    // /v1/stage/screens answers a flat id; /v1/stage/screen/{id}/layout the same shape for the layout.
    private sealed record StageScreen([property: JsonPropertyName("uuid")] string Uuid);

    private sealed record StageLayoutId([property: JsonPropertyName("uuid")] string? Uuid);
}
