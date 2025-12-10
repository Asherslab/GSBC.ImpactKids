using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People.MedicalNotes;
using GSBC.ImpactKids.Shared.Contracts.Entities.Pagination;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.People.MedicalNotes;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.WASM.Extensions;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.People.Features.MedicalNotes.Components;

public partial class CreateMedicalNote : ComponentBase
{
    [Parameter]
    public Guid PersonId { get; set; }

    [Parameter]
    public ICollection<MedicalType>? MedicalTypes { get; set; }

    [Parameter]
    public EventCallback<ICollection<MedicalType>?> MedicalTypesChanged { get; set; }

    private readonly CreateMedicalNoteRequest _createRequest = new();

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();

        _createRequest.PersonId = PersonId;

        if (MedicalTypes == null)
            await RefreshMedicalTypes();
    }

    private async Task RefreshMedicalTypes()
    {
        BasicReadMultipleResponse<MedicalType>? resp = await MedicalTypeService.ReadMultiple(
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

        MedicalTypes = resp.Entities;
        await MedicalTypesChanged.InvokeAsync(MedicalTypes);
        StateHasChanged();
    }

    private bool _creatingMedicalNote;

    public async Task<bool> ExecuteCreateMedicalNote()
    {
        _creatingMedicalNote = true;
        StateHasChanged();
        BasicResponse? resp = await MedicalNoteService.Create(_createRequest);
        _creatingMedicalNote = false;
        
        if (resp.HasErrorOrNull())
        {
            Snackbar.AddErrorResponse(resp);
            return false;
        }

        StateHasChanged();
        return true;
    }
}