using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.People.Pages;

public partial class Individual
{
    [Parameter]
    public Guid Id { get; set; }

    private AsyncData<Person> _person = AsyncData<Person>.NotAsked();

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        PeopleStore.Subscribe(_ => RetrievePerson());

        await Task.WhenAll(
            PeopleStore.RefreshAll()
        );
    }

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();

        RetrievePerson();
    }

    private void RetrievePerson()
    {
        _person = PeopleStore.GetState().First(x => x.Id == Id);
        StateHasChanged();
    }

    // private CancellationTokenSource _refreshMemorisationEntriesTokenSource = new();
    //
    // private async Task RefreshMemorisationEntries()
    // {
    //     _memorisationEntries = null;
    //     StateHasChanged();
    //
    //     await _refreshMemorisationEntriesTokenSource.CancelAsync();
    //     _refreshMemorisationEntriesTokenSource = new CancellationTokenSource();
    //
    //     BasicReadMultipleResponse<MemorisationEntry>? response = await MemorisationEntriesService.ReadMultiple(
    //         new MemorisationEntriesRequest
    //         {
    //             Pagination = PaginationRequest.All(),
    //             
    //             IncludeService = true,
    //             IncludeMemoryVerse = true,
    //
    //             PersonId = Id,
    //             CurrentSchoolTerm = true
    //         },
    //         _refreshMemorisationEntriesTokenSource.Token
    //     );
    //
    //     _memorisationEntries = response?.Entities;
    //     StateHasChanged();
    //     if (response.HasErrorOrNull())
    //     {
    //         Snackbar.AddErrorResponse(response);
    //     }
    // }
}