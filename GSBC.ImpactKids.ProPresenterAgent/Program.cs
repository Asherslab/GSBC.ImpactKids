using System.Net;
using GSBC.ImpactKids.ProPresenterAgent;
using GSBC.ImpactKids.ProPresenterAgent.Credentials;
using GSBC.ImpactKids.ProPresenterAgent.Enrolment;
using GSBC.ImpactKids.ProPresenterAgent.Feeds;
using GSBC.ImpactKids.ProPresenterAgent.ImpactKids;
using GSBC.ImpactKids.ProPresenterAgent.ProPresenter;
using GSBC.ImpactKids.ProPresenterAgent.Stage;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.ProPresenter;
using Microsoft.Extensions.Options;

// The Impact Kids agent on a ProPresenter Mac. Started at login by launchd (see install/install.sh),
// so it has no window: setup happens in the browser, settings come from Impact Kids' admin page, and
// what it serves goes to ProPresenter on this same Mac. See docs/work/2026-10-propresenter-agent.md.

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Logging.AddSimpleConsole(console =>
{
    // launchd sends stdout to ~/Library/Logs/ImpactKids/ProPresenterAgent.log, which is the only
    // place anybody will ever read these - so a timestamp on every line, and one line per entry.
    console.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
    console.SingleLine = true;
});

builder.Services.AddOptions<AgentOptions>()
    .Bind(builder.Configuration.GetSection(AgentOptions.SectionName))
    .Validate(options => options.ImpactKidsUrl != null,
        "Agent__ImpactKidsUrl is not set - this agent does not know which Impact Kids to connect to. " +
        "install.sh sets it; a local run gets it from the AppHost.")
    .ValidateOnStart();

// The feeds are for ProPresenter on this Mac and nothing else. Loopback only: nothing the agent
// serves is ever reachable from the church network.
builder.WebHost.ConfigureKestrel(kestrel => kestrel.Listen(IPAddress.Loopback, ProPresenterFeeds.Port));

builder.Services.AddSingleton<AgentState>();
builder.Services.AddSingleton<CredentialStore>();
builder.Services.AddSingleton<EnrolmentFlow>();
builder.Services.AddSingleton<ImpactKidsClient>();
builder.Services.AddSingleton<FirstTimersFeed>();
builder.Services.AddSingleton<StageDriver>();
builder.Services.AddSingleton<ScheduleFeeds>();

// The event stream stays open all night; Impact Kids' heartbeats keep it moving.
builder.Services.AddHttpClient(EventStreamListener.HttpClientName, http => http.Timeout = Timeout.InfiniteTimeSpan);
builder.Services.AddHostedService<EventStreamListener>();

builder.Services.AddHttpClient<TokenClient>((sp, http) =>
    http.BaseAddress = sp.GetRequiredService<IOptions<AgentOptions>>().Value.ImpactKidsUrl);

// ProPresenter answers in milliseconds on the same Mac or it is not there at all.
builder.Services.AddHttpClient<ProPresenterApi>(http => http.Timeout = TimeSpan.FromSeconds(3));

builder.Services.AddHostedService<AgentWorker>();

WebApplication app = builder.Build();

app.MapGet(ProPresenterFeeds.FirstTimersPath,
    (FirstTimersFeed feed, CancellationToken token) => feed.HandleAsync(token));

app.MapGet(ProPresenterFeeds.NowPath, (ScheduleFeeds feeds, string? group) => feeds.Now(group));
app.MapGet(ProPresenterFeeds.NextPath, (ScheduleFeeds feeds) => feeds.Next());

app.Run();
