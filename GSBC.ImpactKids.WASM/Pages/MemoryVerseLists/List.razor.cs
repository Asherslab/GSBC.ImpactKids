using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scripture.Memorisation;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scripture.Memorisation.MemoryVerses;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.WASM.Components.Base;
using GSBC.ImpactKids.WASM.Components.Dialogs.Create;
using GSBC.ImpactKids.WASM.Components.Dialogs.Update;
using GSBC.ImpactKids.WASM.Extensions;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace GSBC.ImpactKids.WASM.Pages.MemoryVerseLists;

public partial class List : EventListeningComponent
{
    [Parameter]
    public Guid Id { get; set; }

    private MemoryVerseList?         _list;
    private ICollection<MemoryVerse> _memoryVerses = [];

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        await RefreshList();
        await SubscribeToEvent(MemoryVerseList.BuildSubscription(_list?.SchoolTermId, _list?.Id), RefreshList);

        await RefreshMemoryVerses();
        await SubscribeToEvent(MemoryVerse.BuildSubscription(_list?.Id), RefreshMemoryVerses);
    }
    
    
    
    private async Task UpdateList()
    {
        DialogParameters<UpdateMemoryVerseListDialog> parameters = new()
        {
            { x => x.List, _list },
        };

        await DialogService.ShowAsync<UpdateMemoryVerseListDialog>("Update Memory Verse List", parameters);
    }
    
    private async Task DeleteList()
    {
        bool? result = await DialogService.ShowMessageBox(
            "Warning", 
            "Deleting can not be undone!", 
            yesText:"Delete!", cancelText:"Cancel");
        
        if (result == null)
            return;
        
        BasicReadRequest request = new()
        {
            Guid = Id
        };

        await MemoryVerseListsService.Delete(request);
        Navigation.NavigateTo("/terms");
    }

    private async Task RefreshList()
    {
        BasicReadResponse<MemoryVerseList>? response = await MemoryVerseListsService.Read(
            new BasicReadRequest
            {
                Guid = Id
            }
        );

        if (response.HasErrorOrNull())
        {
            Snackbar.AddErrorResponse(response);
            return;
        }

        _list = response.Entity;
        StateHasChanged();
    }

    private async Task RefreshMemoryVerses()
    {
        BasicReadMultipleResponse<MemoryVerse>? response = await MemoryVersesService.ReadMultiple(
            new MemoryVersesRequest
            {
                MemoryVerseListId = Id
            }
        );

        if (response.HasErrorOrNull())
        {
            Snackbar.AddErrorResponse(response);
            return;
        }

        _memoryVerses = response.Entities;
        StateHasChanged();
    }

    private async Task CreateMemoryVerse()
    {
        DialogParameters<CreateMemoryVerseDialog> parameters = new()
        {
            { x => x.List, _list }
        };

        DialogOptions opts = new()
        {
            FullWidth = true,
            MaxWidth = MaxWidth.Large
        };

        await DialogService.ShowAsync<CreateMemoryVerseDialog>("Create Memory Verse", parameters, opts);
    }

    private async Task UpdateMemoryVerse(MemoryVerse memoryVerse)
    {
        DialogParameters<UpdateMemoryVerseDialog> parameters = new()
        {
            { x => x.Verse, memoryVerse },
            { x => x.List, _list }
        };

        DialogOptions opts = new()
        {
            FullWidth = true,
            MaxWidth = MaxWidth.Large
        };

        await DialogService.ShowAsync<UpdateMemoryVerseDialog>("Update Memory Verse", parameters, opts);
    }

    private async Task DeleteMemoryVerse(MemoryVerse memoryVerse)
    {
        bool? result = await DialogService.ShowMessageBox(
            "Warning",
            "Deleting can not be undone!",
            yesText: "Delete!", cancelText: "Cancel");

        if (result == null)
            return;

        BasicReadRequest request = new()
        {
            Guid = memoryVerse.Id
        };

        await MemoryVersesService.Delete(request);
    }
}