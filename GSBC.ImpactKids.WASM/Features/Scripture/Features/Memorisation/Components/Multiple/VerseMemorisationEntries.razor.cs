using GSBC.ImpactKids.Shared.Contracts.Entities.MemoryVerses;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.MemorisationEntries;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.WASM.Extensions;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.Scripture.Features.Memorisation.Components.Multiple;

public partial class VerseMemorisationEntries : ComponentBase
{
    [Parameter]
    public ICollection<MemorisationEntry>? MemorisationEntries { get; set; }
    
    [Parameter]
    public bool ShowPersonName { get; set; }
    
    [Parameter]
    public bool ShowMemoryVerse { get; set; }
    
    [Parameter]
    public bool ShowService { get; set; }

    private ICollection<MemorisationEntry>? _memorisationEntries;
    private bool                            _waitingForEntries;
    
    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        if (MemorisationEntries != null)
        {
            _memorisationEntries = MemorisationEntries;
            _waitingForEntries = false;
        }
        else
        {
            _waitingForEntries = true;
        }
    }

    private async Task UpdateMemorisationEntry(
        MemorisationEntry memorisationEntry,
        bool?             recited         = null,
        bool?             fiveDollaryDoos = null,
        bool?             oneDollaryDoo   = null
    )
    {
        _waitingForEntries = true;
        StateHasChanged();
        UpdateMemorisationEntryRequest request = new()
        {
            PersonId = memorisationEntry.PersonId,
            ServiceId = memorisationEntry.ServiceId,
            MemoryVerseId = memorisationEntry.MemoryVerseId
        };

        if (recited != null)
            request.VerseRecited.Value = recited.Value;

        if (fiveDollaryDoos != null)
            request.FiveDollaryDoosGiven.Value = fiveDollaryDoos.Value;

        if (oneDollaryDoo != null)
            request.OneDollaryDooGiven.Value = oneDollaryDoo.Value;

        BasicResponse? response = await MemorisationEntriesService.Update(request);

        if (response.HasErrorOrNull())
        {
            Snackbar.AddErrorResponse(response);
            _waitingForEntries = false;
            StateHasChanged();
        }
    }
}