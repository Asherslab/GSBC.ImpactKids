using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scripture;
using GSBC.ImpactKids.WASM.Extensions;

namespace GSBC.ImpactKids.WASM.Features.Scripture.Features.Memorisation.Features.MemoryVerses.Components.Individual;

public partial class MemoryVerseOverview
{
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        HandleStateChangeSubscriptionDisposal(ServicesStore);

        await Task.WhenAll(
            EntityStore.RefreshAll(),
            ServicesStore.RefreshAll()
        );
    }

    private IEnumerable<Service> ServiceSearch(string? search, IEnumerable<Service> enumerable)
    {
        if (Entity.Data == null)
            return [];

        search = search?.Replace("/", " "); // makes date searching match better
        
        return enumerable
            .ExceptBy(Entity.Data.ServiceIds, x => x.Id)
            .OrderByDescending(x => x.LocalDate)
            .FuzzySearch(
                query: search,
                threshold: 20,
                orderByBest: true,
                fields:
                [
                    x => x.Name,
                    x => x.LocalDate.ToString("dd"),
                    x => x.LocalDate.ToString("MM"),
                    x => x.LocalDate.ToString("yyyy"),
                ]
            ).Take(5);
    }

    private IEnumerable<BibleVerse> BibleVerseSearch(string? search, IEnumerable<BibleVerse> enumerable)
    {
        if (Entity.Data == null)
            return [];

        enumerable = enumerable.ExceptBy(Entity.Data.BibleVerseIds, x => x.Id);

        if (search == null)
            return [];

        return BibleVerse.BibleVerseSearch(search, enumerable)
            .Take(5);
    }
}