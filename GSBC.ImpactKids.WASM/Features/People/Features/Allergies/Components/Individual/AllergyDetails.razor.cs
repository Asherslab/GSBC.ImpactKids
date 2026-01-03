using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.People.Allergies;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.People.Features.Allergies.Components.Individual;

public partial class AllergyDetails
{
    [Parameter]
    public Guid? PersonId { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        AllergensStore.Subscribe(_ => StateHasChanged());

        await Task.WhenAll(
            EntityStore.RefreshAll(),
            AllergensStore.RefreshAll()
        );
    }

    protected override CreateAllergyRequest ModifyCreateRequest(CreateAllergyRequest request)
    {
        if (PersonId != null)
            request.PersonId = PersonId.Value;
        return request;
    }
}