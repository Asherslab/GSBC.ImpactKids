using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People.Allergies;
using GSBC.ImpactKids.Shared.Contracts.Entities.Pagination;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.People.Allergies;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.WASM.Extensions;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.People.Features.Allergies.Components;

public partial class CreateAllergy : ComponentBase
{
    [Parameter]
    public Guid PersonId { get; set; }

    [Parameter]
    public ICollection<Allergen>? Allergens { get; set; }

    [Parameter]
    public EventCallback<ICollection<Allergen>?> AllergensChanged { get; set; }

    private readonly CreateAllergyRequest _createRequest = new();

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();

        _createRequest.PersonId = PersonId;

        if (Allergens == null)
            await RefreshAllergens();
    }

    private async Task RefreshAllergens()
    {
        BasicReadMultipleResponse<Allergen>? resp = await AllergenService.ReadMultiple(
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

        Allergens = resp.Entities;
        await AllergensChanged.InvokeAsync(Allergens);
        StateHasChanged();
    }

    private bool _creatingAllergy;

    public async Task<bool> ExecuteCreateAllergy()
    {
        _creatingAllergy = true;
        StateHasChanged();
        BasicResponse? resp = await AllergyService.Create(_createRequest);
        _creatingAllergy = false;
        
        if (resp.HasErrorOrNull())
        {
            Snackbar.AddErrorResponse(resp);
            return false;
        }

        StateHasChanged();
        return true;
    }
}