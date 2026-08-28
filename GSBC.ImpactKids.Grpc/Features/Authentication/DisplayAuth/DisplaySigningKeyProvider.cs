using System.Text;
using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Data.Models.Attendance;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace GSBC.ImpactKids.Grpc.Features.Authentication.DisplayAuth;

/// <summary>
/// Holds the current display signing key in memory so JwtBearer can resolve it without
/// touching the database on every request.
/// <para>
/// It has to be a cache rather than a lookup because
/// <see cref="TokenValidationParameters.IssuerSigningKeyResolver"/> is synchronous and this
/// key lives behind an async database read. A wall display reconnects all night, so a read
/// per request would be a query storm for a value that changes only when somebody presses
/// the rotate button.
/// </para>
/// <para>
/// <b>This is what makes rotation total.</b> A rotation mints a new signing key, so every
/// token issued under the old one stops verifying as soon as the new key is loaded here -
/// there is no revocation list and no expiry to wait out. <see cref="RefreshAsync"/> is
/// called directly by the rotation, so in this process the change is immediate; any other
/// replica picks it up within <see cref="RefreshInterval"/>.
/// </para>
/// </summary>
public sealed class DisplaySigningKeyProvider(
    IDbContextFactory<GsbcDbContext>    dbFactory,
    ILogger<DisplaySigningKeyProvider>  logger
) : BackgroundService
{
    /// <summary>
    /// The upper bound on how long a replica that did not serve the rotation keeps honouring
    /// tokens from the old key. Short enough to be "immediate" to somebody watching a wall.
    /// </summary>
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Empty until the first load, and empty again if the key is ever removed. Empty means
    /// every display token fails to verify, which is the correct answer when there is no
    /// enrolment key: nothing has been set up, so nothing is enrolled.
    /// </summary>
    private volatile SecurityKey[] _keys = [];

    /// <summary>
    /// Handed to JwtBearer. Ignores the key id on the token - there is exactly one signing
    /// key at a time, and a token that does not verify against it is by definition a token
    /// from a rotation that has already happened.
    /// </summary>
    public IEnumerable<SecurityKey> Resolve() => _keys;

    public async Task RefreshAsync(CancellationToken token = default)
    {
        await using GsbcDbContext db = await dbFactory.CreateDbContextAsync(token);

        string? signingKey = await db.PickupDisplayKeys
            .AsNoTracking()
            .Select(x => x.TokenSigningKey)
            .FirstOrDefaultAsync(token);

        _keys = string.IsNullOrEmpty(signingKey)
            ? []
            : [new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey))];
    }

    /// <summary>
    /// Loaded once before the app serves anything, so the first display request of a
    /// process never races the first refresh.
    /// </summary>
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await RefreshAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A database that is not up yet must not stop the service starting. The loop
            // below tries again, and until it succeeds displays are simply turned away.
            logger.LogWarning(exception, "Could not load the display signing key at startup");
        }

        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(RefreshInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await RefreshAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Keep the last key that loaded rather than signing every wall out over a
                // blip. A rotation that lands during one takes effect on the next tick.
                logger.LogWarning(exception, "Could not refresh the display signing key");
            }
        }
    }
}
