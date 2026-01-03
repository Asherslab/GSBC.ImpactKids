using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.People.MedicalNotes;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.People.Features.MedicalNotes.Components.Individual;

public partial class MedicalNoteDetails
{
    [Parameter]
    public Guid? PersonId { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        MedicalTypesStore.Subscribe(_ => StateHasChanged());

        await Task.WhenAll(
            EntityStore.RefreshAll(),
            MedicalTypesStore.RefreshAll()
        );
    }

    protected override CreateMedicalNoteRequest ModifyCreateRequest(CreateMedicalNoteRequest request)
    {
        if (PersonId != null)
            request.PersonId = PersonId.Value;
        return request;
    }
}