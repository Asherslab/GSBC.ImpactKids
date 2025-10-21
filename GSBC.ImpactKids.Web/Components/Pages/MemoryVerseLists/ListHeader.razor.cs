using GSBC.ImpactKids.Shared.Contracts.Entities.MemoryVerses;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Web.Components.Dialogs.Update;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace GSBC.ImpactKids.Web.Components.Pages.MemoryVerseLists;

public partial class ListHeader : ComponentBase
{
    [Parameter]
    public required MemoryVerseList List { get; set; }
    
    private async Task UpdateList()
    {
        DialogParameters<UpdateMemoryVerseListDialog> parameters = new()
        {
            { x => x.List, List },
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
            Guid = List.Id
        };

        await MemoryVerseListsService.Delete(request);
        Navigation.NavigateTo("/terms");
    }
}