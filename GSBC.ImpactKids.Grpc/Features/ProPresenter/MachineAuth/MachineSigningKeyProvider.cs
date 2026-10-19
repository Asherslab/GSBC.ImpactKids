using System.Collections.Frozen;
using System.Text;
using GSBC.ImpactKids.Grpc.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace GSBC.ImpactKids.Grpc.Features.ProPresenter.MachineAuth;

/// <summary>
/// Every enrolled machine's signing key, in memory, so JwtBearer can resolve one synchronously -
/// the same constraint, and the same shape, as <c>DisplaySigningKeyProvider</c>.
/// <para>
/// The difference is that there are many keys, one per machine, picked by the token's key id. A
/// token whose machine is not in here does not verify, which is what makes deleting the row total:
/// <see cref="RefreshAsync"/> is called straight after a delete, so in this process a revoked
/// machine is refused on its next call, and any other replica within <see cref="RefreshInterval"/>.
/// </para>
/// </summary>
public sealed class MachineSigningKeyProvider(
    IDbContextFactory<GsbcDbContext>   dbFactory,
    ILogger<MachineSigningKeyProvider> logger
) : BackgroundService
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(15);

    private volatile FrozenDictionary<string, SecurityKey> _keys = FrozenDictionary<string, SecurityKey>.Empty;

    /// <summary>
    /// Handed to JwtBearer. Exactly one key or none - never "try them all", which would let one
    /// machine's key verify a token claiming to be another machine.
    /// </summary>
    public IEnumerable<SecurityKey> Resolve(string? keyId) =>
        keyId != null && _keys.TryGetValue(keyId, out SecurityKey? key) ? [key] : [];

    public async Task RefreshAsync(CancellationToken token = default)
    {
        await using GsbcDbContext db = await dbFactory.CreateDbContextAsync(token);

        // Enrolled machines only. A machine still waiting on its code has a signing key, but no
        // token has ever been minted with it and none should verify.
        var rows = await db.ProPresenterMachines
            .AsNoTracking()
            .Where(x => x.SecretHash != null)
            .Select(x => new { x.Id, x.TokenSigningKey })
            .ToListAsync(token);

        _keys = rows.ToFrozenDictionary(
            x => x.Id.ToString(),
            x => (SecurityKey)new SymmetricSecurityKey(Encoding.UTF8.GetBytes(x.TokenSigningKey)) { KeyId = x.Id.ToString() }
        );
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await RefreshAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Same as the display keys: a database that is not up yet must not stop the service.
            // Machines are turned away until the loop below loads the keys.
            logger.LogWarning(exception, "Could not load the ProPresenter machine signing keys at startup");
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
                logger.LogWarning(exception, "Could not refresh the ProPresenter machine signing keys");
            }
        }
    }
}
