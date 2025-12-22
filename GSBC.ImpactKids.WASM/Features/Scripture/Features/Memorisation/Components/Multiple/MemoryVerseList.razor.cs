using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scripture.Memorisation;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.Scripture.Features.Memorisation.Components.Multiple;

public partial class MemoryVerseList : ComponentBase
{
    [Parameter]
    public ICollection<MemoryVerse>? MemoryVerses { get; set; }
    
    [Parameter]
    public Guid? ServiceId { get; set; }
    
    // used to keep existing entities visible while they are updating in background
    private ICollection<MemoryVerse>? _memoryVerses;
    private bool                      _waitingForUpdate;
    
    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        if (MemoryVerses != null)
        {
            _memoryVerses = MemoryVerses;
            _waitingForUpdate = false;
        }
        else
        {
            _waitingForUpdate = true;
        }
    }
}