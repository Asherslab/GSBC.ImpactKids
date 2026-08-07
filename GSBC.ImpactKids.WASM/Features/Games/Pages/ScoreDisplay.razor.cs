using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Games;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Features.Games;
using GSBC.ImpactKids.Shared.Contracts.Services.Features.Games;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.Games.Pages;

/// <summary>
/// Unauthenticated wall display. Polls rather than using the event stream, because
/// the stream needs a signed in cookie and this screen has none.
/// </summary>
public partial class ScoreDisplay : IAsyncDisposable
{
    private static readonly TimeSpan PollEvery = TimeSpan.FromSeconds(2);

    [Parameter]
    public Guid? ServiceId { get; set; }

    [Inject]
    public required IGameDisplayService DisplayService { get; set; }

    private GameScoreboardResponse? _board;

    private CancellationTokenSource? _pollTokenSource;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        await PollOnceAsync();

        StartPolling();
    }

    private void StartPolling()
    {
        _pollTokenSource?.Cancel();
        _pollTokenSource = new CancellationTokenSource();
        CancellationToken token = _pollTokenSource.Token;

        _ = Task.Run(async () =>
            {
                using PeriodicTimer timer = new(PollEvery);

                while (await timer.WaitForNextTickAsync(token))
                {
                    await PollOnceAsync();
                }
            },
            token
        );
    }

    private async Task PollOnceAsync()
    {
        try
        {
            GameScoreboardResponse resp = await DisplayService.GetScoreboard(
                new GameScoreboardRequest { ServiceId = ServiceId }
            );

            if (resp is null)
                return;

            _board = resp;
        }
        catch
        {
            // Keep the last good board on screen rather than blanking the wall.
            return;
        }

        await InvokeAsync(StateHasChanged);
    }

    /// <summary>Bar length is relative to the leader, so the board reads at a distance.</summary>
    private string BarStyle(TeamScoreLine line)
    {
        int max = _board?.Teams.Max(x => Math.Max(x.DisplayPoints, 0)) ?? 0;

        int percent = max <= 0
            ? 0
            : (int)Math.Round(Math.Max(line.DisplayPoints, 0) / (double)max * 100);

        return $"width: {percent}%;";
    }

    public async ValueTask DisposeAsync()
    {
        if (_pollTokenSource is not null)
        {
            await _pollTokenSource.CancelAsync();
            _pollTokenSource.Dispose();
            _pollTokenSource = null;
        }
    }
}
