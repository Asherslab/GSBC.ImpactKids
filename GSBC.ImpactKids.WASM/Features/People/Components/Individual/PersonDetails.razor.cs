using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People;
using GSBC.ImpactKids.Shared.Contracts.Entities.Pagination;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.People;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.WASM.Extensions;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.People.Components.Individual;

public partial class PersonDetails : ComponentBase
{
    [Parameter]
    public required Person? Person { get; set; }

    [Parameter]
    public bool Editing { get; set; }

    private UpdatePersonRequest       _updateRequest = new();
    private bool                      _readonly      = true;
    private ICollection<SchoolGrade>? _schoolGrades;
    private bool                      _waitingForRefresh;
    
    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();
        if (Person != null)
        {
            _updateRequest = new UpdatePersonRequest
            {
                Guid = Person.Id
            };

            _updateRequest.FirstName.SetInitialValue(Person.FirstName);
            _updateRequest.LastName.SetInitialValue(Person.LastName);

            _updateRequest.SchoolGradeId.SetInitialValue(Person.SchoolGrade?.Id);
            _updateRequest.MediaConsent.SetInitialValue(Person.MediaConsent);
            _updateRequest.LocalDateOfBirth.SetInitialValue(Person.LocalDateOfBirth);
            _updateRequest.LocalFirstTime.SetInitialValue(Person.LocalFirstTime);
            _waitingForRefresh = false;
        }
        else
        {
            _waitingForRefresh = true;
        }

        if (_schoolGrades == null)
            await RefreshSchoolGrades();

        _readonly = !Editing;
    }

    private async Task RefreshSchoolGrades()
    {
        BasicReadMultipleResponse<SchoolGrade>? resp = await SchoolGradeService.ReadMultiple(
            new BasicReadMultipleRequest
            {
                Pagination = PaginationRequest.All()
            }
        );

        if (resp.HasErrorOrNull())
        {
            Snackbar.AddErrorResponse(resp);
            return;
        }

        _schoolGrades = resp.Entities;
        StateHasChanged();
    }

    public async Task<bool> UpdatePersonDetails()
    {
        if (_updateRequest.Guid == Guid.Empty)
            return false;

        _waitingForRefresh = true;
        StateHasChanged();
        BasicResponse? resp = await PersonService.Update(_updateRequest);

        if (resp.HasErrorOrNull())
        {
            _waitingForRefresh = false;
            Snackbar.AddErrorResponse(resp);
            return false;
        }
        
        StateHasChanged();
        return true;
    }
}