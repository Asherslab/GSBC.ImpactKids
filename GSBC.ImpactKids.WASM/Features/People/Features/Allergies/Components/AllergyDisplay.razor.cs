using GSBC.ImpactKids.Shared.Contracts.Entities.People;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace GSBC.ImpactKids.WASM.Features.People.Features.Allergies.Components;

public partial class AllergyDisplay : ComponentBase
{
    [Parameter]
    public Allergy? Allergy { get; set; }

    [Parameter]
    public bool None { get; set; }

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
}