using GSBC.ImpactKids.Shared.Contracts.Entities.People;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace GSBC.ImpactKids.WASM.Features.People.Features.MedicalNotes.Components;

public partial class MedicalNoteDisplay : ComponentBase
{
    [Parameter]
    public MedicalNote? MedicalNote { get; set; }

    [Parameter]
    public bool None { get; set; }

    private string? AvatarDisplay() => None
        ? "N"
        : MedicalNote?.MedicalType[0].ToString();

    private string DisplayText() => None
        ? "No Medical Notes"
        : MedicalNote == null
            ? "Type"
            : $"{MedicalNote.MedicalType}";

    private Color AvatarColor() => None
        ? Color.Success
        : MedicalNote == null
            ? Color.Default
            : MedicalNote.Severe
                ? Color.Error
                : Color.Primary;
}