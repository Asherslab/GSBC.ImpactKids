using GSBC.ImpactKids.Shared.Contracts.Entities.MemoryVerses;
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
    
    [Parameter]
    public ICollection<MemorisationEntry>? MemorisationEntries { get; set; }

    private PersonDetails? _personDetailsComponent;
    private bool           _editingDetails;

    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        if (Person == null)
        {
            if (_editingDetails)
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
        }
    }

    private async Task UpdatePerson()
    {
        if (_editingDetails && _personDetailsComponent != null)
        {
            bool success = await _personDetailsComponent.UpdatePersonDetails();
            if (success)
            {
                _editingDetails = false;
            }
        }
    }
}