using System.Collections.Immutable;
using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People.MedicalNotes;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.WASM.Extensions;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace GSBC.ImpactKids.WASM.Features.People.Features.MedicalNotes.Components.Individual;

public partial class MedicalNoteDisplay : ComponentBase
{
    [Parameter]
    public required Guid? Id { get; set; }

    [Parameter]
    public bool None { get; set; }

    [Parameter]
    public bool AllowDeleting { get; set; }

    private AsyncData<MedicalNote> _medicalNote = AsyncData<MedicalNote>.NotAsked();

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        MedicalNotesStore.Subscribe(_ => RetrieveMedicalNote());

        await Task.WhenAll(
            MedicalNotesStore.RefreshAll(),
            MedicalTypesStore.RefreshAll()
        );
    }

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();

        RetrieveMedicalNote();
    }

    private string? _avatarDisplay;
    private Color   _avatarColor = Color.Default;
    private string  _displayText = "Medical not requested";

    private void RetrieveMedicalNote()
    {
        AsyncData<ImmutableList<MedicalNote>> medicalNotes = MedicalNotesStore.GetState().Entities;
        AsyncData<ImmutableList<MedicalType>> medicalTypes = MedicalTypesStore.GetState().Entities;

        if (!medicalNotes.HasData)
        {
            _medicalNote = _medicalNote.CopyStatus(medicalNotes);
            StateHasChanged();
            return;
        }

        if (!medicalTypes.HasData)
        {
            _medicalNote = _medicalNote.CopyStatus(medicalTypes);
            StateHasChanged();
            return;
        }

        _avatarDisplay = "N";

        MedicalNote? medicalNote = medicalNotes.Data!
            .FirstOrDefault(x => x.Id == Id);

        MedicalType? type = medicalTypes.Data!
            .FirstOrDefault(x => x.Id == medicalNote?.MedicalTypeId);

        _medicalNote = medicalNote == null
            ? _medicalNote.ToFailure("Failed to find Medical Note")
            : _medicalNote.ToSuccess(medicalNote);

        _avatarDisplay = None
            ? "N"
            : type?.Label[0].ToString();

        _displayText = None
            ? "Medical not requested"
            : _medicalNote.Data == null
                ? "Type"
                : type?.Label ?? "Other";

        _avatarColor = None
            ? Color.Error
            : _medicalNote.Data == null
                ? Color.Default
                : _medicalNote.Data.Severe
                    ? Color.Error
                    : type?.Label == "None"
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
        _medicalNote = _medicalNote.ToLoading();
        StateHasChanged();

        BasicResponse? resp = await MedicalNoteService.Delete(
            new BasicReadRequest
            {
                Guid = Id.Value
            }
        );

        if (resp.HasErrorOrNull())
        {
            Snackbar.AddErrorResponse(resp);
            RetrieveMedicalNote();
        }
    }
}