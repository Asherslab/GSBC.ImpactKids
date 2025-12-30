using System.Collections.Immutable;
using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People.MedicalNotes;
using GSBC.ImpactKids.WASM.Extensions;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.People.Features.MedicalNotes.Components.Multiple;

public partial class MedicalNotesList : ComponentBase
{
    [Parameter]
    public Func<MedicalNote, bool>? Filter { get; set; }

    [Parameter]
    public Guid? PersonId { get; set; }

    [Parameter]
    public ICollection<MedicalNote>? MedicalNotes { get; set; }

    private AsyncData<ImmutableList<Guid>> _medicalNoteIds = AsyncData<ImmutableList<Guid>>.NotAsked();

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        MedicalNotesStore.Subscribe(_ => FilterMedicalNotes());

        await Task.WhenAll(
            MedicalNotesStore.RefreshAll()
        );
        FilterMedicalNotes();
    }

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();

        FilterMedicalNotes();
    }

    private void FilterMedicalNotes()
    {
        AsyncData<ImmutableList<MedicalNote>> medicalNotes = MedicalNotesStore.GetState().Entities;

        if (medicalNotes.Data == null)
        {
            _medicalNoteIds = _medicalNoteIds.CopyStatus(medicalNotes);
            return;
        }

        IEnumerable<MedicalNote> filteredMedicalNotes = medicalNotes.Data;

        if (Filter != null)
        {
            filteredMedicalNotes = filteredMedicalNotes
                .Where(Filter);
        }

        if (PersonId != null)
        {
            filteredMedicalNotes = filteredMedicalNotes
                .Where(x =>
                    x.PersonId == PersonId
                );
        }

        _medicalNoteIds = _medicalNoteIds.ToSuccess(filteredMedicalNotes
            .Select(x => x.Id)
            .ToImmutableList()
        );

        StateHasChanged();
    }
}