using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People.MedicalNotes;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.WASM.Extensions;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace GSBC.ImpactKids.WASM.Features.People.Features.MedicalNotes.Components;

public partial class MedicalNoteDisplay : ComponentBase
{
    [Parameter]
    public MedicalNote? MedicalNote { get; set; }

    [Parameter]
    public bool None { get; set; }
    
    [Parameter]
    public bool AllowDeleting { get; set; }

    private string? AvatarDisplay() => None
        ? "N"
        : MedicalNote?.MedicalType[0].ToString();

    private string DisplayText() => None
        ? "Medical not requested"
        : MedicalNote == null
            ? "Type"
            : $"{MedicalNote.MedicalType}";

    private Color AvatarColor() => None
        ? Color.Error
        : MedicalNote == null
            ? Color.Default
            : MedicalNote.Severe
                ? Color.Error
                : MedicalNote.MedicalType == "None"
                    ? Color.Success
                    : Color.Primary;

    private bool _hasBeenDeleted;
    private async Task OnDelete()
    {
        if (MedicalNote?.Id == null)
            return;

        bool? result = await DialogService.ShowMessageBox(
            "Warning",
            "Deleting can not be undone!",
            yesText: "Delete!", cancelText: "Cancel");

        if (result == null)
            return;
        _hasBeenDeleted = true;
        StateHasChanged();
        
        BasicResponse? resp = await MedicalNoteService.Delete(
            new BasicReadRequest
            {
                Guid = MedicalNote.Id
            }
        );

        if (resp.HasErrorOrNull())
        {
            Snackbar.AddErrorResponse(resp);
            _hasBeenDeleted = false;
        }
    }
}