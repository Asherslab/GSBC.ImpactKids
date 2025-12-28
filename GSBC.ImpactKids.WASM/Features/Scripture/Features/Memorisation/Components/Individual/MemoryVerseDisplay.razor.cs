using System.Collections.Immutable;
using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scripture.Memorisation;
using GSBC.ImpactKids.WASM.Extensions;
using Microsoft.AspNetCore.Components;
using MudBlazor.Utilities;

namespace GSBC.ImpactKids.WASM.Features.Scripture.Features.Memorisation.Components.Individual;

public partial class MemoryVerseDisplay : ComponentBase
{
    [Parameter]
    public Guid? Id { get; set; }

    [Parameter]
    public string? Href { get; set; }

    [Parameter]
    public bool ShowReference { get; set; }

    [Parameter]
    public bool ShowVerseText { get; set; }

    [Parameter]
    public bool ShowOriginalText { get; set; }

    [Parameter]
    public bool ShowOriginalTextButton { get; set; }

    [Parameter]
    public int Elevation { set; get; } = 6;

    private AsyncData<MemoryVerse> _memoryVerse = AsyncData<MemoryVerse>.NotAsked();

    private string ReferenceOnlyClass => CssBuilder.Empty()
        .AddClass("clickable mud-ripple", Href != null)
        .AddClass("d-flex justify-start flex-direction-row flex-grow-1")
        .Build();

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        MemoryVersesStore.Subscribe(_ => RetrieveService());

        await Task.WhenAll(
            MemoryVersesStore.RefreshAll()
        );
    }

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();

        RetrieveService();
    }

    private void RetrieveService()
    {
        AsyncData<ImmutableList<MemoryVerse>> memoryVerses = MemoryVersesStore.GetState().Entities;

        if (!memoryVerses.HasData)
        {
            _memoryVerse = _memoryVerse.CopyStatus(memoryVerses);
            return;
        }

        MemoryVerse? memoryVerse = memoryVerses.Data!
            .FirstOrDefault(x => x.Id == Id);

        _memoryVerse = memoryVerse == null
            ? _memoryVerse.ToFailure("Failed to find Memory Verse")
            : _memoryVerse.ToSuccess(memoryVerse);

        StateHasChanged();
    }
}