namespace GSBC.ImpactKids.Grpc.Services;

/// <summary>
/// In-process fan-out for "something a wall display renders has moved", so a watching
/// display can be pushed to instead of polling.
/// <para>
/// Fed by <see cref="Features.Eventing.Services.RabbitWorker"/>, which receives the same
/// fanout every instance gets, so it does not matter which replica took the write or which
/// one the display happens to be connected to.
/// </para>
/// <para>
/// Deliberately carries no payload - a waiter re-reads for itself. Each display's query is
/// small, so there is nothing to gain from threading the change through.
/// </para>
/// <para>
/// One subclass per display, registered as a singleton, so a change to the scores does not
/// wake the pickup wall and vice versa.
/// </para>
/// </summary>
public abstract class DataChangeNotifier
{
    private readonly Lock _gate = new();

    private TaskCompletionSource _signal = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Registers interest before doing any reading. Anything notified from now until the
    /// returned subscription is awaited still wakes it, so a change that lands while the
    /// caller is mid-read is never dropped.
    /// </summary>
    public DataChangeSubscription Subscribe()
    {
        lock (_gate)
        {
            return new DataChangeSubscription(_signal.Task);
        }
    }

    public void NotifyChanged()
    {
        TaskCompletionSource signal;

        // Swap the source before completing it, so a waiter woken by this change is
        // already queued against the next one and cannot miss it.
        lock (_gate)
        {
            signal = _signal;
            _signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        signal.TrySetResult();
    }
}

/// <summary>A claim on the next change. Await it once; take a fresh one for the next wait.</summary>
public readonly struct DataChangeSubscription(Task signal)
{
    /// <summary>
    /// Waits for the change this subscription was taken against. Returns false if
    /// <paramref name="timeout"/> elapsed first, which callers use as a keepalive tick.
    /// </summary>
    public async Task<bool> WaitAsync(TimeSpan timeout, CancellationToken token)
    {
        using CancellationTokenSource timer = CancellationTokenSource.CreateLinkedTokenSource(token);

        Task finished = await Task.WhenAny(signal, Task.Delay(timeout, timer.Token));

        // Whichever won, drop the timer rather than leaving it to fire later.
        await timer.CancelAsync();

        token.ThrowIfCancellationRequested();

        return finished == signal;
    }
}
