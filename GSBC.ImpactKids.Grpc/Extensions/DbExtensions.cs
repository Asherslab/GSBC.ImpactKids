using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using GSBC.ImpactKids.Grpc.Conversion;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Extensions;

public static class DbExtensions
{
    public static async IAsyncEnumerable<BasicReadMultipleResponse<T>> ReturnInBatches<T, TDb>(
        this IQueryable<TDb>                       query,
        IConverter<TDb, T>                         converter,
        int                                        batchSize = 5000,
        [EnumeratorCancellation] CancellationToken token     = default
    ) where TDb : class
    {
        List<TDb> dbEntities = await query.AsNoTracking().ToListAsync(token);
        int       total      = dbEntities.Count;
        List<T>   entities   = dbEntities.Select(converter.Convert).ToList();

        foreach (int i in Enumerable.Range(0, (int)Math.Ceiling((double)total / batchSize)))
        {
            ImmutableList<T> batch = entities
                .Skip(i * batchSize)
                .Take(batchSize)
                .ToImmutableList();

            yield return new BasicReadMultipleResponse<T>
            {
                Success = true,
                Entities = batch
            };
        }
    }
}