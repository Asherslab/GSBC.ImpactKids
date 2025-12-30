using System.Collections.Immutable;
using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People.Allergies;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.WASM.Extensions;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace GSBC.ImpactKids.WASM.Features.People.Features.Allergies.Components.Individual;

public partial class AllergyDisplay : ComponentBase
{
    [Parameter]
    public required Guid? Id { get; set; }

    [Parameter]
    public bool None { get; set; }

    [Parameter]
    public bool AllowDeleting { get; set; }

    private AsyncData<Allergy> _allergy = AsyncData<Allergy>.NotAsked();

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        AllergiesStore.Subscribe(_ => RetrieveAllergy());

        await Task.WhenAll(
            AllergiesStore.RefreshAll(),
            AllergensStore.RefreshAll()
        );
    }

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();

        RetrieveAllergy();
    }

    private string? _avatarDisplay;
    private Color   _avatarColor = Color.Default;
    private string  _displayText = "Allergies not requested";

    private void RetrieveAllergy()
    {
        AsyncData<ImmutableList<Allergy>> allergies = AllergiesStore.GetState().Entities;
        AsyncData<ImmutableList<Allergen>> allergens = AllergensStore.GetState().Entities;

        if (!allergies.HasData)
        {
            _allergy = _allergy.CopyStatus(allergies);
            StateHasChanged();
            return;
        }

        if (!allergens.HasData)
        {
            _allergy = _allergy.CopyStatus(allergens);
            StateHasChanged();
            return;
        }

        _avatarDisplay = "N";

        Allergy? allergy = allergies.Data!
            .FirstOrDefault(x => x.Id == Id);

        Allergen? allergen = allergens.Data!
            .FirstOrDefault(x => x.Id == allergy?.AllergenId);

        _allergy = allergy == null
            ? _allergy.ToFailure("Failed to find Allergy")
            : _allergy.ToSuccess(allergy);

        _avatarDisplay = None
            ? "N"
            : allergen?.Label[0].ToString();

        _displayText = None
            ? "Allergies not requested"
            : _allergy.Data == null
                ? "Type"
                : allergen?.Label ?? "Other";

        _avatarColor = None
            ? Color.Error
            : _allergy.Data == null
                ? Color.Default
                : _allergy.Data.Severe
                    ? Color.Error
                    : allergen?.Label == "None"
                        ? Color.Success
                        : Color.Primary;

        StateHasChanged();
    }

    private async Task OnDelete()
    {
        if (Id == null)
            return;

        bool? result = await DialogService.ShowMessageBox(
            "Warning",
            "Deleting can not be undone!",
            yesText: "Delete!", cancelText: "Cancel");

        if (result == null)
            return;
        _allergy = _allergy.ToLoading();
        StateHasChanged();

        BasicResponse? resp = await AllergyService.Delete(
            new BasicReadRequest
            {
                Guid = Id.Value
            }
        );

        if (resp.HasErrorOrNull())
        {
            Snackbar.AddErrorResponse(resp);
            RetrieveAllergy();
        }
    }
}