using GSBC.ImpactKids.Web.Components.Dialogs.Create;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.Web.Components.Pages.Terms;

public partial class IndexHeader
{
    [Parameter]
    public DateTime? Date { get; set; }

    [Parameter]
    public required EventCallback<DateTime?> OnDateChanged { get; set; }
    
    private async Task CreateSchoolTerm()
    {
        await DialogService.ShowAsync<CreateSchoolTermDialog>("Create School Term");
    }
}