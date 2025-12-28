using System.Collections.Immutable;
using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;
using GSBC.ImpactKids.WASM.Services.RefreshableStore;

namespace GSBC.ImpactKids.WASM.Features.Scheduling.Features.Services.Features.ServiceTypes;

public record MultipleServiceTypesState(
    AsyncData<ImmutableList<ServiceType>> FilteredServiceTypes,
    string?                               Search
) : IInitialisableState<MultipleServiceTypesState>
{
    public static MultipleServiceTypesState Initial => new(AsyncData<ImmutableList<ServiceType>>.NotAsked(), null);

    public MultipleServiceTypesState SetSearch(string? search) => this with { Search = search };
}