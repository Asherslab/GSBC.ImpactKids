using System.Collections.Immutable;
using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People.Allergies;
using GSBC.ImpactKids.WASM.Components.Common;
using GSBC.ImpactKids.WASM.Components.Common.Inputs;
using GSBC.ImpactKids.WASM.Extensions;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace GSBC.ImpactKids.WASM.Features.People.Features.Allergies.Components.Individual;

public partial class AllergyDisplay
{
    [Parameter]
    public bool None { get; set; }

    [Parameter]
    public bool AllowUpdating { get; set; }

    [Parameter]
    public bool AllowDeleting { get; set; }

    private AsyncData<Allergen> _allergen = AsyncData<Allergen>.NotAsked();

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        await Task.WhenAll(
            EntityStore.RefreshAll(),
            AllergensStore.RefreshAll()
        );
    }

    private string? _avatarDisplay;
    private Color   _avatarColor = Color.Default;
    private string  _displayText = "Allergies not requested";

    protected override void OnRetrievedEntity()
    {
        AsyncData<ImmutableList<Allergen>> allergens = AllergensStore.GetState().Entities;

        if (!allergens.HasData)
        {
            _allergen = _allergen.CopyStatus(allergens);
            StateHasChanged();
            return;
        }

        _avatarDisplay = "N";

        Allergen? allergen = allergens.Data!
            .FirstOrDefault(x => x.Id == Entity.Data!.AllergenId);
        

        _allergen = allergen == null
            ? _allergen.ToFailure("Failed to find Allergen")
            : _allergen.ToSuccess(allergen);

        _avatarDisplay = None
            ? "N"
            : allergen != null
                ? allergen.Label[0].ToString()
                : "O";

        _displayText = None
            ? "Allergies not requested"
            : Entity.Data == null
                ? "Allergen"
                : allergen?.Label ?? "Other";

        _avatarColor = None
            ? Color.Error
            : Entity.Data == null
                ? Color.Default
                : Entity.Data.Severe
                    ? Color.Error
                    : allergen?.Label == "None"
                        ? Color.Success
                        : Color.Primary;
    }

    private async Task OnUpdate() =>
        await DetailsComponentDialog.Open<AllergyDetails>(
            DialogService,
            "Update Allergy",
            ModificationState.Updating,
            Id
        );

    private async Task OnDelete() =>
        await DeleteWithDialog(
            AllergyService,
            Entity.Data?.Id,
            () => Entity = Entity.ToLoading(),
            RetrieveEntity
        );
}