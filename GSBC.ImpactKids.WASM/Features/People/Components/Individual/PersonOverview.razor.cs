using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People.Allergies;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People.MedicalNotes;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scripture.Memorisation;
using GSBC.ImpactKids.WASM.Features.People.Features.Allergies.Components;
using GSBC.ImpactKids.WASM.Features.People.Features.MedicalNotes.Components;
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

    private PersonDetails? _personDetailsComponent;
    private bool           _editingDetails;

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

    private ICollection<MedicalType>? _medicalTypes;
    private CreateMedicalNote?        _createMedicalNoteComponent;
    private bool                      _creatingMedicalNote;

    private async Task CreateMedicalNote()
    {
        if (_creatingMedicalNote && _createMedicalNoteComponent != null)
        {
            bool success = await _createMedicalNoteComponent.ExecuteCreateMedicalNote();
            if (success)
            {
                _creatingMedicalNote = false;
            }
        }
    }

    private ICollection<Allergen>? _allergens;
    private CreateAllergy?         _createAllergyComponent;
    private bool                   _creatingAllergy;

    private async Task CreateAllergy()
    {
        if (_creatingAllergy && _createAllergyComponent != null)
        {
            bool success = await _createAllergyComponent.ExecuteCreateAllergy();
            if (success)
            {
                _creatingAllergy = false;
            }
        }
    }
}