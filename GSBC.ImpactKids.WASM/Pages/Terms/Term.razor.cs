using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.School;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scripture.Memorisation;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.DollarStoreEntries;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.School.SchoolTerms;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scripture.Memorisation.MemoryVerseLists;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.WASM.Components.Base;
using GSBC.ImpactKids.WASM.Components.Dialogs.Create;
using GSBC.ImpactKids.WASM.Components.Dialogs.Update;
using GSBC.ImpactKids.WASM.Extensions;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace GSBC.ImpactKids.WASM.Pages.Terms;

public partial class Term : EventListeningComponent
{
    [Parameter]
    public Guid? Id { get; set; }

    private SchoolTerm?                    _term;
    private ICollection<MemoryVerseList>?  _lists;
    private ICollection<DollarStoreEntry>? _dollarStoreEntries;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        await RefreshTerm();
        await SubscribeToEvent(SchoolTerm.BuildSubscription(_term?.Id), RefreshTerm);

        await Task.WhenAll(RefreshMemoryVerseLists(), RefreshDollarStoreEntries());
        await SubscribeToEvent(MemoryVerseList.BuildSubscription(_term?.Id), RefreshMemoryVerseLists);
        await SubscribeToEvent(DollarStoreEntry.BuildSubscription(), RefreshDollarStoreEntries);
    }

    private async Task RefreshTerm()
    {
        BasicReadResponse<SchoolTerm>? response = await SchoolTermsService.Read(
            new SchoolTermRequest
            {
                Guid = Id ?? Guid.Empty,
                ThisTerm = Id == null
            }
        );

        if (response.HasErrorOrNull())
        {
            Snackbar.AddErrorResponse(response);
            return;
        }

        _term = response.Entity;
        StateHasChanged();
    }

    private async Task RefreshDollarStoreEntries()
    {
        BasicReadMultipleResponse<DollarStoreEntry>? response = await DollarStoreEntryService.ReadMultiple(
            new DollarStoreEntriesRequest()
            {
                SchoolTermId = Id
            }
        );

        if (response.HasErrorOrNull())
        {
            Snackbar.AddErrorResponse(response);
            return;
        }

        _dollarStoreEntries = response.Entities;
        StateHasChanged();
    }

    private async Task RefreshMemoryVerseLists()
    {
        BasicReadMultipleResponse<MemoryVerseList>? response = await MemoryVerseListsService.ReadMultiple(
            new MemoryVerseListsRequest
            {
                SchoolTermId = Id
            }
        );

        if (response.HasErrorOrNull())
        {
            Snackbar.AddErrorResponse(response);
            return;
        }

        _lists = response.Entities;
        StateHasChanged();
    }

    private async Task CreateMemoryVerseList()
    {
        DialogParameters<CreateMemoryVerseListDialog> parameters = new()
        {
            { x => x.SchoolTerm, _term }
        };

        DialogOptions opts = new()
        {
            FullWidth = true
        };

        await DialogService.ShowAsync<CreateMemoryVerseListDialog>("Create Memory Verse List", parameters, opts);
    }
    
    private async Task CreateDollarStoreEntry()
    {
        await DialogService.ShowAsync<CreateDollarStoreEntryDialog>("Create Dollar Store Entry");
    }

    private async Task UpdateSchoolTerm()
    {
        DialogParameters<UpdateSchoolTermDialog> parameters = new()
        {
            { x => x.Term, _term }
        };

        await DialogService.ShowAsync<UpdateSchoolTermDialog>("Update School Term", parameters);
    }
    
    private async Task DeleteSchoolTerm()
    {
        bool? result = await DialogService.ShowMessageBox(
            "Warning",
            "Deleting can not be undone!",
            yesText: "Delete!", cancelText: "Cancel");

        if (result == null || _term == null)
            return;

        BasicReadRequest request = new()
        {
            Guid = _term.Id
        };

        await SchoolTermsService.Delete(request);
        Navigation.NavigateTo("/terms");
    }
}