using GSBC.ImpactKids.Shared.Contracts.Entities.People;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace GSBC.ImpactKids.WASM.Features.People.Components.Individual;

public partial class PersonOverview : ComponentBase
{
    [Parameter]
    public required Person? Person { get; set; }

    [Parameter]
    public ICollection<Person>? FamilyMembers { get; set; }

    private PersonDetails? _personDetailsComponent;
    private bool           _editingDetails;
    private bool           _editReloading;

    private async Task UpdatePerson()
    {
        if (_editingDetails && _personDetailsComponent != null)
        {
            bool success = await _personDetailsComponent.UpdatePersonDetails();
            if (success)
            {
                _editReloading = true;
                StateHasChanged();
            }
        }
    }

    public void PersonUpdated()
    {
        if (_editingDetails && !_editReloading)
        {
            Snackbar.Add(
                "Somebody else has made modifications to this family, your edit has been cancelled",
                Severity.Warning,
                x =>
                {
                    x.CloseAfterNavigation = true;
                    x.VisibleStateDuration = int.MaxValue;
                });
        }

        _editingDetails = false;
        _editReloading = false;
        _personDetailsComponent?.PersonUpdated();
    }
}