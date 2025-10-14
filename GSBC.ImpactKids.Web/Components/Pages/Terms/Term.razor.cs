using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.SchoolTerms;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.Web.Components.Base;
using GSBC.ImpactKids.Web.Extensions;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.Web.Components.Pages.Terms;

public partial class Term : EventListeningComponent
{
    [Parameter]
    public Guid? Id { get; set; }

    private SchoolTerm? _term;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        await RefreshTerm();
        await SubscribeToEvent($"{nameof(SchoolTerm)}.{Id}", RefreshTerm);
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
}