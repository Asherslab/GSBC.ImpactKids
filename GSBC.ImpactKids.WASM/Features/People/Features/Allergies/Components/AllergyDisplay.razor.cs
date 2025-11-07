using GSBC.ImpactKids.Shared.Contracts.Entities.People;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.WASM.Extensions;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace GSBC.ImpactKids.WASM.Features.People.Features.Allergies.Components;

public partial class AllergyDisplay : ComponentBase
{
    [Parameter]
    public Allergy? Allergy { get; set; }

    [Parameter]
    public bool None { get; set; }
    
    [Parameter]
    public bool AllowDeleting { get; set; }

    private string? AvatarDisplay() => None
        ? "N"
        : Allergy?.Allergen[0].ToString();

    private string DisplayText() => None
        ? "Allergies not requested"
        : Allergy == null
            ? "Allergen"
            : $"{Allergy.Allergen}";

    private Color AvatarColor() => None
        ? Color.Error
        : Allergy == null
            ? Color.Default
            :  Allergy.Severe
                ? Color.Error
                : Allergy.Allergen == "None"
                    ? Color.Success
                    : Color.Primary;

    private bool _hasBeenDeleted;
    private async Task OnDelete()
    {
        if (Allergy?.Id == null)
            return;

        bool? result = await DialogService.ShowMessageBox(
            "Warning",
            "Deleting can not be undone!",
            yesText: "Delete!", cancelText: "Cancel");

        if (result == null)
            return;
        _hasBeenDeleted = true;
        StateHasChanged();
        
        BasicResponse? resp = await AllergyService.Delete(
            new BasicReadRequest
            {
                Guid = Allergy.Id
            }
        );

        if (resp.HasErrorOrNull())
        {
            Snackbar.AddErrorResponse(resp);
            _hasBeenDeleted = false;
        }
    }
}