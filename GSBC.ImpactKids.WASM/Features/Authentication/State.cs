using System.Collections.Immutable;
using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.WASM.Services.RefreshableStore;

namespace GSBC.ImpactKids.WASM.Features.Authentication;

public record MultipleUsersState(
    AsyncData<ImmutableList<User>> FilteredUsers,
    string?                        Search
) : IInitialisableState<MultipleUsersState>
{
    public static MultipleUsersState Initial => new(
        AsyncData<ImmutableList<User>>.NotAsked(),
        null
    );

    public MultipleUsersState SetSearch(string? search) => this with { Search = search };
}