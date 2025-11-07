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
}