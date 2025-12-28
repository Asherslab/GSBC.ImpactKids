using System.Collections.Immutable;
using System.Text.Json;
using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scripture.Memorisation;
using GSBC.ImpactKids.WASM.Extensions;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.Scripture.Features.Memorisation.Components.Multiple;

public partial class MemoryVerseList : ComponentBase
{
    [Parameter]
    public Func<MemoryVerse, bool>? Filter { get; set; }

    [Parameter]
    public Guid? ServiceId { get; set; }

    private AsyncData<ImmutableList<Guid>> _memoryVerseIds = AsyncData<ImmutableList<Guid>>.NotAsked();

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        MemoryVersesStore.Subscribe(_ => FilterMemoryVerses());

        await Task.WhenAll(
            MemoryVersesStore.RefreshAll()
        );
        FilterMemoryVerses();
    }

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();

        FilterMemoryVerses();
    }

    private void FilterMemoryVerses()
    {
        AsyncData<ImmutableList<MemoryVerse>> memoryVerses = MemoryVersesStore.GetState().Entities;

        if (memoryVerses.Data == null)
        {
            _memoryVerseIds = _memoryVerseIds.CopyStatus(memoryVerses);
            return;
        }

        List<MemoryVerse> filteredMemoryVerses = memoryVerses.Data.ToList();

        Console.WriteLine($"Test 1: {JsonSerializer.Serialize(filteredMemoryVerses)}");
        if (Filter != null)
        {
            filteredMemoryVerses = filteredMemoryVerses
                .Where(Filter)
                .ToList();
        }

        if (ServiceId != null)
        {
            filteredMemoryVerses = filteredMemoryVerses
                .Where(x => x.ServiceIds.Contains(ServiceId.Value))
                .ToList();
        }

        Console.WriteLine($"Test 2: {JsonSerializer.Serialize(filteredMemoryVerses)}");
        _memoryVerseIds = _memoryVerseIds.ToSuccess(filteredMemoryVerses
            .Select(x => x.Id)
            .ToImmutableList()
        );

        StateHasChanged();
    }
}