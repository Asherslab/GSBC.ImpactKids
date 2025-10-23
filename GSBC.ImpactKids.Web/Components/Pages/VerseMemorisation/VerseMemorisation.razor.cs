using GSBC.ImpactKids.Web.Components.Base;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.Web.Components.Pages.VerseMemorisation;

public partial class VerseMemorisation : EventListeningComponent
{
    [Parameter]
    public Guid? ServiceId { get; set; }
}