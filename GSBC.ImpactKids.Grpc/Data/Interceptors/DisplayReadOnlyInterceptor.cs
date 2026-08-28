using Grpc.Core;
using GSBC.ImpactKids.Grpc.Features.Authentication.DisplayAuth;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GSBC.ImpactKids.Grpc.Data.Interceptors;

/// <summary>
/// A wall display may read. It may never write, and this is where that is <b>enforced</b>
/// rather than reviewed.
/// <para>
/// The policies in <see cref="Policies"/> cannot tell a read from a write - they see a
/// method name, not what it does - so <see cref="Policies.EnabledOrDisplay"/> on a write
/// method would quietly hand a screen the ability to sign children out. This interceptor is
/// the layer that does not care what the attributes say: if the caller on the current
/// request is a display, nothing it does reaches the database.
/// </para>
/// <para>
/// Background writers are unaffected - the RabbitMQ worker, the heartbeat and the Elvanto
/// sync have no HttpContext, so there is no principal here to object to.
/// </para>
/// <para>
/// <b>Known limit:</b> a SaveChanges interceptor does not see <c>ExecuteUpdateAsync</c>,
/// <c>ExecuteDeleteAsync</c> or raw SQL. The two such calls in this service are both on the
/// Elvanto sync path, which no display policy reaches, so the gap is currently closed by
/// where those calls happen rather than by this class. Adding a bulk write to a method a
/// display can call would slip past this - put it behind <see cref="Policies.EnabledOnly"/>.
/// </para>
/// </summary>
public class DisplayReadOnlyInterceptor(
    IHttpContextAccessor                  httpContextAccessor,
    ILogger<DisplayReadOnlyInterceptor>   logger
) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData      eventData,
        InterceptionResult<int> result
    )
    {
        ThrowIfDisplay();

        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData      eventData,
        InterceptionResult<int> result,
        CancellationToken       cancellationToken = default
    )
    {
        ThrowIfDisplay();

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>
    /// The generation claim is present on display tokens and on nothing else, so it is the
    /// test for "this caller is a screen". A request with no principal at all - a background
    /// worker, a startup task - is not a display and passes straight through.
    /// <para>
    /// <b>Reaching this is always a bug in the policy list</b>, never something a correctly
    /// configured display can do - so it logs an error naming the path. Verified on
    /// 2026-08-28 by deliberately opening a delete to a display: the write was refused and
    /// the row survived, but the caller saw <c>grpc-status: 2</c> ("Exception was thrown by
    /// handler") rather than the PermissionDenied thrown here, because the handler wraps it.
    /// That is why the log line matters - the status on the wire is not a usable diagnostic.
    /// </para>
    /// </summary>
    private void ThrowIfDisplay()
    {
        HttpContext? httpContext = httpContextAccessor.HttpContext;

        bool isDisplay = httpContext?.User
            .HasClaim(claim => claim.Type == DisplayAuthDefaults.GenerationClaimType) == true;

        if (!isDisplay)
            return;

        logger.LogError(
            "A display reached a write on {Path}. The policy list let it through - that method should "
            + "not be marked {Policy}",
            httpContext!.Request.Path,
            nameof(Policies.EnabledOrDisplay)
        );

        throw new RpcException(new Status(
            StatusCode.PermissionDenied,
            "A display is read only."
        ));
    }
}
