using System.Collections.Immutable;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Sync;
using GSBC.ImpactKids.WASM.Services.RefreshableStore;

namespace GSBC.ImpactKids.WASM.Features.Sync;

public record IndividualSyncState(
    string?                         Search,
    ImmutableHashSet<SyncEventType> SelectedEventTypes,
    ImmutableHashSet<string>        SelectedFields
) : IInitialisableState<IndividualSyncState>
{
    public static IndividualSyncState Initial => new(
        null,
        ImmutableHashSet<SyncEventType>.Empty,
        ImmutableHashSet<string>.Empty
    );

    public IndividualSyncState SetSearch(string? search)                       => this with { Search             = search };
    public IndividualSyncState SetEventTypes(ImmutableHashSet<SyncEventType> t) => this with { SelectedEventTypes = t };
    public IndividualSyncState SetFields(ImmutableHashSet<string> fields)      => this with { SelectedFields      = fields };
}
