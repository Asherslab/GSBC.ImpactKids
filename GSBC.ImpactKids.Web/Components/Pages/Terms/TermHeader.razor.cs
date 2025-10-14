using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Web.Components.Dialogs.Update;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace GSBC.ImpactKids.Web.Components.Pages.Terms;

public partial class TermHeader : ComponentBase
{
    [Parameter]
    public required SchoolTerm SchoolTerm { get; set; }
    
    private async Task UpdateSchoolTerm()
    {
        DialogParameters<UpdateSchoolTermDialog> parameters = new()
        {
            { x => x.Term, SchoolTerm }
        };

        await DialogService.ShowAsync<UpdateSchoolTermDialog>("Update School Term", parameters);
    }
    
    private async Task DeleteSchoolTerm()
    {
        bool? result = await DialogService.ShowMessageBox(
            "Warning", 
            "Deleting can not be undone!", 
            yesText:"Delete!", cancelText:"Cancel");
        
        if (result == null)
            return;
        
        BasicReadRequest request = new()
        {
            Guid = SchoolTerm.Id
        };

        await SchoolTermsService.Delete(request);
        Navigation.NavigateTo("/terms");
    }
}