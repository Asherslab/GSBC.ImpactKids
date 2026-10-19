using System.Buffers.Text;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using GSBC.ImpactKids.ProPresenterAgent.Credentials;
using GSBC.ImpactKids.ProPresenterAgent.ImpactKids;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.ProPresenter;
using Microsoft.Extensions.Options;

namespace GSBC.ImpactKids.ProPresenterAgent.Enrolment;

/// <summary>
/// First boot, or after being revoked: get this Mac approved by a leader. OAuth 2.0 for native apps
/// (RFC 8252) with PKCE (RFC 7636) - listen on a loopback port, open the browser on Impact Kids'
/// connect page, wait for the redirect carrying a single-use code, and exchange it.
/// <para>
/// The verifier never leaves this process; only its hash went out in the link. A code that reaches
/// anybody else - a browser extension, a log - is worthless to them.
/// </para>
/// </summary>
public sealed class EnrolmentFlow(
    IOptions<AgentOptions>  options,
    TokenClient             tokens,
    ILogger<EnrolmentFlow>  logger
)
{
    /// <summary>How long one opened browser tab is waited on before the flow gives up and starts over.</summary>
    private static readonly TimeSpan AttemptTimeout = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Between attempts. Long, because each attempt opens a browser on what may be the projector Mac;
    /// a refused or ignored approval must not turn into a tab every minute during a service.
    /// </summary>
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(10);

    public async Task<MachineCredential> RunAsync(CancellationToken stoppingToken)
    {
        while (true)
        {
            try
            {
                MachineCredential? credential = await AttemptAsync(stoppingToken);

                if (credential != null)
                    return credential;
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(exception, "Setting this Mac up with Impact Kids failed");
            }

            logger.LogInformation("Trying again in {Delay}", RetryDelay);
            await Task.Delay(RetryDelay, stoppingToken);
        }
    }

    private async Task<MachineCredential?> AttemptAsync(CancellationToken stoppingToken)
    {
        string verifier    = RandomUrlSafe(32);
        string state       = RandomUrlSafe(16);
        int    port        = FreeLoopbackPort();
        string redirectUri = $"http://127.0.0.1:{port}/callback";

        using HttpListener listener = new();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();

        string connectUrl = new Uri(options.Value.ImpactKidsUrl, ProPresenterEnrolment.ConnectPath)
                            + "?redirect_uri=" + Uri.EscapeDataString(redirectUri)
                            + "&state=" + state
                            + "&code_challenge=" + ProPresenterEnrolment.ComputeChallenge(verifier)
                            + "&code_challenge_method=" + ProPresenterEnrolment.ChallengeMethod
                            + "&name=" + Uri.EscapeDataString(ComputerName());

        logger.LogInformation(
            "This Mac is not connected to Impact Kids yet. Opening the browser to approve it - if it does not open, visit {Url}",
            connectUrl);

        OpenBrowser(connectUrl);

        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        timeout.CancelAfter(AttemptTimeout);

        while (true)
        {
            HttpListenerContext context;

            try
            {
                context = await listener.GetContextAsync().WaitAsync(timeout.Token);
            }
            catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogInformation("Nobody approved this Mac within {Timeout}", AttemptTimeout);
                return null;
            }

            // Browsers ask for a favicon, and anything else may knock. Only the callback counts.
            if (context.Request.Url?.AbsolutePath != "/callback")
            {
                await RespondAsync(context, HttpStatusCode.NotFound, "Not found.");
                continue;
            }

            // A callback this flow did not start - a stale tab, or something forging one. Ignored
            // without spending anything, and the real one can still arrive.
            if (context.Request.QueryString["state"] != state)
            {
                await RespondAsync(context, HttpStatusCode.BadRequest,
                    "This link is out of date. Use the most recent tab Impact Kids opened.");
                continue;
            }

            if (context.Request.QueryString["error"] is { } error)
            {
                logger.LogInformation("Connecting this Mac was cancelled in the browser ({Error})", error);
                await RespondAsync(context, HttpStatusCode.OK,
                    "Cancelled. This Mac is not connected. The tool will ask again later.");
                return null;
            }

            string? code = context.Request.QueryString["code"];

            if (string.IsNullOrEmpty(code))
            {
                await RespondAsync(context, HttpStatusCode.BadRequest, "The approval did not include a code.");
                return null;
            }

            try
            {
                MachineCredential credential = await tokens.ExchangeCodeAsync(code, verifier, redirectUri, stoppingToken);

                logger.LogInformation("Connected to Impact Kids as machine {MachineId}", credential.MachineId);
                await RespondAsync(context, HttpStatusCode.OK,
                    "Connected. This Mac is now set up with Impact Kids - you can close this tab.");

                return credential;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "Exchanging the approval code failed");
                await RespondAsync(context, HttpStatusCode.BadGateway,
                    "Impact Kids approved this Mac, but finishing the connection failed. The tool will try again shortly.");
                return null;
            }
        }
    }

    private static async Task RespondAsync(HttpListenerContext context, HttpStatusCode status, string message)
    {
        byte[] body = Encoding.UTF8.GetBytes(
            "<!doctype html><meta charset=\"utf-8\"><title>Impact Kids ProPresenter</title>"
            + "<body style=\"font-family:-apple-system,sans-serif;max-width:32rem;margin:4rem auto;line-height:1.5\">"
            + $"<h2>Impact Kids ProPresenter</h2><p>{WebUtility.HtmlEncode(message)}</p></body>");

        context.Response.StatusCode = (int)status;
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.ContentLength64 = body.Length;

        await context.Response.OutputStream.WriteAsync(body);
        context.Response.Close();
    }

    /// <summary>Asks the OS for a free port, then lets it go for the listener. The gap is a few microseconds on a Mac nobody else is using.</summary>
    private static int FreeLoopbackPort()
    {
        TcpListener probe = new(IPAddress.Loopback, 0);
        probe.Start();

        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();

        return port;
    }

    private static string RandomUrlSafe(int bytes) => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(bytes));

    private void OpenBrowser(string url)
    {
        try
        {
            using Process? process = Process.Start(new ProcessStartInfo("open", [url]) { UseShellExecute = false });
        }
        catch (Exception exception)
        {
            // Not fatal - the URL is in the log line above.
            logger.LogWarning(exception, "Could not open the browser");
        }
    }

    /// <summary>The name in System Settings → Sharing ("Kids Room Mac"), not the hostname, as the suggested machine name.</summary>
    private static string ComputerName()
    {
        try
        {
            using Process process = Process.Start(new ProcessStartInfo("scutil", ["--get", "ComputerName"])
            {
                RedirectStandardOutput = true,
                UseShellExecute = false
            })!;

            string name = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();

            if (process.ExitCode == 0 && name.Length > 0)
                return name;
        }
        catch
        {
            // Falls through to the hostname.
        }

        return Environment.MachineName;
    }
}
