using System.Collections.Immutable;
using EasyAppDev.Blazor.Store.AsyncActions;

namespace GSBC.ImpactKids.WASM.Services.RefreshableStore;

public record EntityListState<T>(
    AsyncData<ImmutableList<T>> Entities
)
{
    public static EntityListState<T> Initial => new(AsyncData<ImmutableList<T>>.NotAsked());
}