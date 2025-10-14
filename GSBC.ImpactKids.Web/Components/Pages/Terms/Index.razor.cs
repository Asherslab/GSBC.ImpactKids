using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.SchoolTerms;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.Web.Components.Base;
using GSBC.ImpactKids.Web.Components.Dialogs.Update;
using GSBC.ImpactKids.Web.Extensions;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace GSBC.ImpactKids.Web.Components.Pages.Terms;

public partial class Index : EventListeningComponent
{
    [SupplyParameterFromQuery]
    public int? Year { get; set; }

    private DateTime? _date;

    private ICollection<SchoolTerm>? _terms;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        if (Year == null)
        {
            Year = DateTime.Now.Year;
            SetQueryParameters();
        }
        _date = new DateTime(Year.Value, 1, 1);

        await RefreshTerms();
        await SubscribeToEvent(SchoolTerm.BuildSubscription(), RefreshTerms);
    }

    private async Task RefreshTerms()
    {
        BasicReadMultipleResponse<SchoolTerm>? response = await
            SchoolTermsService.ReadMultiple(new SchoolTermsRequest
            {
                Year = Year
            });

        if (response.HasErrorOrNull())
        {
            Snackbar.AddErrorResponse(response);
            return;
        }

        _terms = response.Entities;
        StateHasChanged();
    }

    private async Task UpdateSchoolTerm(SchoolTerm term)
    {
        DialogParameters<UpdateSchoolTermDialog> parameters = new()
        {
            { x => x.Term, term }
        };

        await DialogService.ShowAsync<UpdateSchoolTermDialog>("Update School Term", parameters);
    }

    private async Task DeleteSchoolTerm(SchoolTerm term)
    {
        bool? result = await DialogService.ShowMessageBox(
            "Warning", 
            "Deleting can not be undone!", 
            yesText:"Delete!", cancelText:"Cancel");
        
        if (result == null)
            return;
        
        BasicReadRequest request = new()
        {
            Guid = term.Id
        };

        await SchoolTermsService.Delete(request);
    }

    private async Task OnDateChanged(DateTime? date)
    {
        _date = date;
        Year = date?.Year;
        SetQueryParameters();
        await RefreshTerms();
    }

    private void SetQueryParameters()
    {
        Navigation.NavigateTo(GetQueryParameters());
    }

    private string GetQueryParameters()
    {
        return Navigation.GetUriWithQueryParameters(new Dictionary<string, object?>
        {
            [nameof(Year)] = Year
        });
    }
}