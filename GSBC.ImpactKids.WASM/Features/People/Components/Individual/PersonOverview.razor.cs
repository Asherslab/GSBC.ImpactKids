using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.People.Components.Individual;

public partial class PersonOverview
{
    [Parameter]
    public required Guid? Id { get; set; }

    [Parameter]
    public bool HideMemorisationEntries { get; set; }

    [Parameter]
    public bool HideFamilyMembers { get; set; }

    [Parameter]
    public EventCallback<bool> ErrorsChanged { get; set; }

    private readonly Dictionary<string, object?> _personDetailsExtraParams = new();

    public PersonOverview()
    {
        _personDetailsExtraParams.Add(nameof(PersonDetails.ErrorsChanged),
            new Func<bool, Task>(PersonDetailsErrorsChanged));
    }

    private bool? _detailsErrors;
    private bool? _medicalNotesNoIds;
    private bool? _allergiesNoIds;

    private async Task SendErrorsChanged()
    {
        if (_detailsErrors == null ||
            _allergiesNoIds == null ||
            _medicalNotesNoIds == null)
            return;

        bool error = _detailsErrors.Value ||
                     _allergiesNoIds.Value ||
                     _medicalNotesNoIds.Value;

        await ErrorsChanged.InvokeAsync(error);
    }

    private async Task PersonDetailsErrorsChanged(bool errors)
    {
        _detailsErrors = errors;
        await SendErrorsChanged();
    }

    private async Task MedicalNotesNoIdsChanged(bool noIds)
    {
        _medicalNotesNoIds = noIds;
        await SendErrorsChanged();
    }

    private async Task AllergiesNoIdsChanged(bool noIds)
    {
        _allergiesNoIds = noIds;
        await SendErrorsChanged();
    }
}