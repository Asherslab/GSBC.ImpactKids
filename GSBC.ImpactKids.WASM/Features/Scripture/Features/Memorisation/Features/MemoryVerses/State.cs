using System.Collections.Immutable;
using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scripture.Memorisation;
using GSBC.ImpactKids.WASM.Services.RefreshableStore;

namespace GSBC.ImpactKids.WASM.Features.Scripture.Features.Memorisation.Features.MemoryVerses;

public record MultipleMemoryVersesState(
    AsyncData<ImmutableList<MemoryVerseList>> FilteredMemoryVerseLists
) : IInitialisableState<MultipleMemoryVersesState>
{
    public static MultipleMemoryVersesState Initial => new(
        AsyncData<ImmutableList<MemoryVerseList>>.NotAsked()
    );
}