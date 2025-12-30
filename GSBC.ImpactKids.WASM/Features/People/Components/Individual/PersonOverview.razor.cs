using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People.Allergies;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People.MedicalNotes;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scripture.Memorisation;
using GSBC.ImpactKids.WASM.Components.Common.Inputs;
using GSBC.ImpactKids.WASM.Features.People.Features.Allergies.Components.Individual;
using GSBC.ImpactKids.WASM.Features.People.Features.MedicalNotes.Components.Individual;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace GSBC.ImpactKids.WASM.Features.People.Components.Individual;

public partial class PersonOverview : ComponentBase
{
    [Parameter]
    public required Guid? Id { get; set; }

    [Parameter]
    public ICollection<MemorisationEntry>? MemorisationEntries { get; set; }

    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        PeopleStore.Subscribe(_ =>
            {
                if (_detailsSent || _detailsState != ModificationState.Updating) return;

                Snackbar.Add(
                    "Somebody else has made modifications to this family, your edit has been cancelled",
                    Severity.Warning,
                    x =>
                    {
                        x.CloseAfterNavigation = true;
                        x.VisibleStateDuration = int.MaxValue;
                    });
                _detailsState = ModificationState.Reading;
                StateHasChanged();
            }
        );
    }

    private bool              _detailsSent;
    private ModificationState _detailsState = ModificationState.Reading;
    private PersonDetails?    _personDetailsComponent;

    private async Task UpdatePerson()
    {
        if (_detailsState == ModificationState.Updating && _personDetailsComponent != null)
        {
            _detailsSent = true;
            try
            {
                bool success = await _personDetailsComponent.UpdatePerson();
                if (success)
                    _detailsState = ModificationState.Reading;
            }
            finally
            {
                _detailsSent = false;
            }
        }
    }

    private async Task DeletePerson()
    {
        if (_personDetailsComponent != null)
        {
            await _personDetailsComponent.DeletePerson();
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