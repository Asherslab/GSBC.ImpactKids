using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Entities.MemoryVerses;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.MemoryVerseLists;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.SchoolTerms;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.Web.Components.Base;
using GSBC.ImpactKids.Web.Components.Dialogs.Create;
using GSBC.ImpactKids.Web.Components.Dialogs.Update;
using GSBC.ImpactKids.Web.Extensions;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace GSBC.ImpactKids.Web.Components.Pages.Terms;

public partial class Term : EventListeningComponent
{
    [Parameter]
    public Guid? Id { get; set; }

    private SchoolTerm?                   _term;
    private ICollection<MemoryVerseList>? _lists;

    private Guid? _selectedMemoryVerseList;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        await RefreshTerm();
        await SubscribeToEvent(SchoolTerm.BuildSubscription(_term?.Id), RefreshTerm);

        await RefreshMemoryVerseLists();
        await SubscribeToEvent(MemoryVerseList.BuildSubscription(_term?.Id), RefreshMemoryVerseLists);
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
    
    private async Task UpdateMemoryVerseList(MemoryVerseList list)
    {
        DialogParameters<UpdateMemoryVerseListDialog> parameters = new()
        {
            { x => x.List, list },
            { x => x.SchoolTerm, _term }
        };

        await DialogService.ShowAsync<UpdateMemoryVerseListDialog>("Update Memory Verse List", parameters);
    }
    
    private async Task DeleteMemoryVerseList(MemoryVerseList list)
    {
        bool? result = await DialogService.ShowMessageBox(
            "Warning", 
            "Deleting can not be undone!", 
            yesText:"Delete!", cancelText:"Cancel");
        
        if (result == null)
            return;
        
        BasicReadRequest request = new()
        {
            Guid = list.Id
        };

        await MemoryVerseListsService.Delete(request);
    }
}