using GSBC.ImpactKids.Grpc.Data;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Extensions;

public static class DbFactoryExtensions
{
    public static async Task<T> RunWithNewDbContext<T>(
        this IDbContextFactory<GsbcDbContext> factory,
        Func<GsbcDbContext, Task<T>>          func,
        CancellationToken                     token = default
    )
    {
        await using GsbcDbContext context = await factory.CreateDbContextAsync(token);

        return await func(context);
    }
}