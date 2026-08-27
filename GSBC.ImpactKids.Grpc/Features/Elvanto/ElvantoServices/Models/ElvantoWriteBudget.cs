namespace GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Models;

/// <summary>
/// A hard ceiling on how many mutations may leave this process, counted across every sync run for
/// the lifetime of the process rather than per run. Per run would be no protection at all for the
/// thing it exists to prevent: a run that goes wrong and is then repeated would get a fresh
/// allowance each time.
///
/// This is deliberately blunt. It does not know what a create is, or which person is being written,
/// and it cannot be talked round by any calling code - it counts sends and stops. It is the
/// mechanism that makes "only one write can happen" a fact about the process rather than a claim
/// about the logic above it.
///
/// <c>Elvanto:MaxWrites</c> unset means no ceiling, which is the normal steady state once writes
/// are trusted; <c>AllowWrites</c> remains the master switch. Setting it to a number is for a
/// controlled first write, where the count is the point.
/// </summary>
public class ElvantoWriteBudget(int? maxWrites)
{
    private int _used;

    public int? MaxWrites => maxWrites;
    public int  Used      => Volatile.Read(ref _used);

    /// <summary>
    /// Consumes one write. False once the ceiling is reached, and it never replenishes - restarting
    /// the process is the only reset, so an exhausted budget cannot quietly come back mid-session.
    /// </summary>
    public bool TryConsume()
    {
        if (maxWrites is null) return true;

        // Increment first, then compare, so two concurrent calls cannot both see the last slot.
        int used = Interlocked.Increment(ref _used);
        if (used <= maxWrites.Value) return true;

        // Undo so Used reports what was actually spent rather than counting refusals.
        Interlocked.Decrement(ref _used);
        return false;
    }
}
