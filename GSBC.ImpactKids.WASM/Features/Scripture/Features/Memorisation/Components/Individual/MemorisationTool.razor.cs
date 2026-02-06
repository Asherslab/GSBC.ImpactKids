using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scripture.Memorisation;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.Scripture.Features.Memorisation.Components.Individual;

public partial class MemorisationTool
{
    [Parameter]
    public Guid? ServiceId { get; set; }

    [Parameter]
    public Guid? MemoryVerseId { get; set; }

    [Parameter]
    public EventCallback<Guid?> MemoryVerseIdChanged { get; set; }

    private string?   _search;
    private string[]? _searchStrings;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        MemoryVersesStore.Subscribe(_ =>
        {
            RetrieveMemoryVerseIfNone();
            StateHasChanged();
        });

        await Task.WhenAll(
            MemoryVersesStore.RefreshAll()
        );
    }

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();

        await RetrieveMemoryVerseIfNone();
    }

    private Task RetrieveMemoryVerseIfNone()
    {
        if (MemoryVerseId != null || ServiceId == null)
            return Task.CompletedTask;

        AsyncData<MemoryVerse> memoryVerse =
            MemoryVersesStore.GetState().First(x => x.ServiceIds.Contains(ServiceId.Value));

        if (!memoryVerse.HasData)
            return Task.CompletedTask;

        MemoryVerseId = memoryVerse.Data!.Id;
        return OnMemoryVerseChanged(MemoryVerseId);
    }

    private async Task OnMemoryVerseChanged(Guid? memoryVerseId)
    {
        MemoryVerseId = memoryVerseId;
        await MemoryVerseIdChanged.InvokeAsync(memoryVerseId);
    }

    private void OnSearchChanged(string? search)
    {
        _search = search;
        _searchStrings = _search?.Split(" ");
    }

    private bool PersonFilter(Person person)
    {
        if (_searchStrings == null)
            return true;

        return _searchStrings.All(x =>
            person.FirstName.Contains(x, StringComparison.InvariantCultureIgnoreCase) ||
            person.LastName.Contains(x, StringComparison.InvariantCultureIgnoreCase)
        );
    }
}