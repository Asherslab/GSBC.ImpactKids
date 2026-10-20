using Grpc.Core;
using GSBC.ImpactKids.ProPresenterAgent.Credentials;
using GSBC.ImpactKids.ProPresenterAgent.Enrolment;
using GSBC.ImpactKids.ProPresenterAgent.ImpactKids;
using GSBC.ImpactKids.ProPresenterAgent.ProPresenter;
using GSBC.ImpactKids.ProPresenterAgent.Songs;
using GSBC.ImpactKids.ProPresenterAgent.Stage;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.ProPresenter;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.ProPresenter;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

namespace GSBC.ImpactKids.ProPresenterAgent;

/// <summary>
/// The agent's life: get connected if it is not, then every 30 seconds pull its configuration,
/// report how it and ProPresenter are doing - its timers and libraries included, for the admin page
/// to choose from - re-read tonight's schedule, and copy the songs when they are due. The report is the heartbeat the admin page's "online" is
/// worked out from. Changes that must land at once (a press of Next) come over the event stream
/// instead; see <see cref="EventStreamListener"/>.
/// </summary>
public sealed class AgentWorker(
    CredentialStore        credentials,
    EnrolmentFlow          enrolment,
    ImpactKidsClient       impactKids,
    ProPresenterApi        proPresenter,
    StageDriver            stage,
    SongSync               songs,
    AgentState             state,
    ILogger<AgentWorker>   logger
) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Impact Kids ProPresenter agent {Version} starting", AgentOptions.Version);

        while (!stoppingToken.IsCancellationRequested)
        {
            MachineCredential? credential = credentials.Load();

            if (credential == null)
            {
                state.Connected = false;
                state.Configuration = null;

                credential = await enrolment.RunAsync(stoppingToken);
                credentials.Save(credential);
            }

            impactKids.UseCredential(credential);
            state.Connected = true;

            try
            {
                await RunConnectedAsync(stoppingToken);
            }
            catch (CredentialRevokedException)
            {
                // Final, and the only thing that is. Forget the credential and ask to be approved
                // again - which opens a browser, so it happens only on this definite answer.
                logger.LogWarning("Impact Kids no longer recognises this Mac - it was disconnected on the admin page. Setting up again");

                credentials.Delete();
                impactKids.UseCredential(null);
            }
        }
    }

    private async Task RunConnectedAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(Interval);

        do
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (CredentialRevokedException)
            {
                throw;
            }
            catch (RpcException exception) when (exception.StatusCode == StatusCode.Unauthenticated)
            {
                // The token, not the credential: expired early, or signed by a key the server has not
                // loaded yet. The next tick asks for a new one; only the token endpoint can say revoked.
                logger.LogWarning("Impact Kids refused the access token; fetching a new one");
                impactKids.UseCredential(credentials.Load());
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
            {
                // Impact Kids or the internet is down. Keep serving what we last knew and try again.
                logger.LogWarning("Could not reach Impact Kids: {Message}", exception.Message);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task TickAsync(CancellationToken token)
    {
        await impactKids.EnsureAuthenticatedAsync(token);

        BasicReadResponse<ProPresenterAgentConfiguration> configuration =
            await impactKids.Agent.GetConfiguration(new ProPresenterAgentRequest(), token);

        if (configuration is { Success: true, Entity: not null })
        {
            if (state.Configuration != configuration.Entity)
                logger.LogInformation(
                    "Configuration: {Name}, ProPresenter at {Host}:{Port}",
                    configuration.Entity.Name, configuration.Entity.ProPresenterHost, configuration.Entity.ProPresenterPort);

            state.Configuration = configuration.Entity;
        }

        string?                          proPresenterVersion = await proPresenter.GetVersionAsync(token);
        List<ProPresenterApi.TimerId>?   timers              = proPresenterVersion == null ? null : await proPresenter.GetTimersAsync(token);
        List<ProPresenterApi.LibraryId>? libraries           = proPresenterVersion == null ? null : await proPresenter.GetLibrariesAsync(token);

        await impactKids.Agent.ReportStatus(new ReportProPresenterAgentStatusRequest
        {
            AgentVersion = AgentOptions.Version,
            ProPresenterReachable = proPresenterVersion != null,
            ProPresenterVersion = proPresenterVersion,
            Timers = timers?.Select(t => new ProPresenterTimer { Uuid = t.Uuid, Name = t.Name }).ToList() ?? [],
            Libraries = libraries?.Select(l => new ProPresenterLibrary { Uuid = l.Uuid, Name = l.Name }).ToList() ?? []
        }, token);

        // The event stream normally brings changes within a second; this is the fallback if it is down.
        await stage.RefreshAsync(token);

        // Last, so a slow read of a big library never holds up the stage.
        await songs.SyncIfDueAsync(libraries, token);
    }
}
