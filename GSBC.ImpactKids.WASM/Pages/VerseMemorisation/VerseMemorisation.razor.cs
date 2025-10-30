using GSBC.ImpactKids.WASM.Components.Base;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Pages.VerseMemorisation;

public partial class VerseMemorisation : EventListeningComponent
{
    [Parameter]
    public Guid? ServiceId { get; set; }
}