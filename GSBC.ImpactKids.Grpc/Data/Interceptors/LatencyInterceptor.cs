using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GSBC.ImpactKids.Grpc.Data.Interceptors;

public sealed class LatencyInterceptor(
    TimeSpan delay
) : DbCommandInterceptor
{
    // Async reader (LINQ-to-Entities queries typically go through this path)
    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand                        command,
        CommandEventData                 eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken                cancellationToken = default
    )
    {
        await DelayIfApplicable(command, cancellationToken);
        return await base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    // Async non-query (INSERT/UPDATE/DELETE)
    public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand               command,
        CommandEventData        eventData,
        InterceptionResult<int> result,
        CancellationToken       cancellationToken = new()
    )
    {
        await DelayIfApplicable(command, cancellationToken);
        return await base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }

    // Scalar (COUNT, MIN/MAX, etc.)
    public override async ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand                  command,
        CommandEventData           eventData,
        InterceptionResult<object> result,
        CancellationToken          cancellationToken = new()
    )
    {
        await DelayIfApplicable(command, cancellationToken);
        return await base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
    }

    private Task DelayIfApplicable(DbCommand command, CancellationToken ct)
    {
        // Basic example: only delay SELECT statements
        // You can get more granular by looking at command.CommandText, parameters,
        // EF Core query tags, etc.
        if (command.CommandText.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            return Task.Delay(delay, ct);

        return Task.CompletedTask;
    }
}