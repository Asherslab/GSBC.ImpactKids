using System.Collections.Immutable;
using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.WASM.Extensions;

namespace GSBC.ImpactKids.WASM.Services.RefreshableStore;

public record EntityListState<T>(
    AsyncData<ImmutableList<T>> Entities
) : IInitialisableState<EntityListState<T>>
{
    public static EntityListState<T> Initial => new(AsyncData<ImmutableList<T>>.NotAsked());

    public AsyncData<T> First(Func<T, bool> predicate)
    {
        AsyncData<T> asyncData = AsyncData<T>.NotAsked();
        if (!Entities.HasData)
        {
            asyncData = asyncData.CopyStatus(Entities);
            return asyncData;
        }

        T? entity = Entities.Data!
            .FirstOrDefault(predicate);

        return entity == null
            ? AsyncData<T>.Failure($"Failed to find {typeof(T).Name}")
            : AsyncData<T>.Success(entity);
    }
}