using System.Collections.Immutable;
using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People;
using GSBC.ImpactKids.WASM.Services.RefreshableStore;

namespace GSBC.ImpactKids.WASM.Features.People;

public record MultiplePeopleState(
    AsyncData<ImmutableList<Person>> FilteredPeople,
    string?                          Search
) : IInitialisableState<MultiplePeopleState>
{
    public static MultiplePeopleState Initial => new(
        AsyncData<ImmutableList<Person>>.NotAsked(),
        null
    );

    public MultiplePeopleState SetSearch(string? search) => this with { Search = search };
}