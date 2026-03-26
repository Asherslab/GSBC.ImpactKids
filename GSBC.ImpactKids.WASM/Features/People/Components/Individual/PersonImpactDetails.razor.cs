using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.People.Components.Individual;

public partial class PersonImpactDetails
{
    [Parameter]
    public Func<bool, Task>? ErrorsChanged { get; set; }
    
    private bool? _mediaConsentError;
    private bool? _firstTimeError;
    
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        HandleSubscriptionDisposal(SchoolGradesStore, _ => StateHasChanged());
        HandleStateChangeSubscriptionDisposal(SchoolGradesStore);

        await Task.WhenAll(
            EntityStore.RefreshAll(),
            SchoolGradesStore.RefreshAll()
        );
    }

    private async Task SendErrorsChanged()
    {
        if (_mediaConsentError == null ||
            _firstTimeError == null)
            return;
        
        bool error = _mediaConsentError.Value ||
                     _firstTimeError.Value;
        if (ErrorsChanged != null)
            await ErrorsChanged(error);
    }

    private async Task<bool> MediaConsentGetError(MediaConsent mediaConsent)
    {
        bool mediaConsentError = mediaConsent == MediaConsent.NotRequested;
        if (_mediaConsentError == mediaConsentError) 
            return mediaConsentError;
        
        _mediaConsentError = mediaConsentError;
        await SendErrorsChanged();
        return mediaConsentError;
    }
    
    private async Task<bool> FirstTimeGetError(DateTime? dateTime)
    {
        bool firstTimeError = dateTime == null;
        if (_firstTimeError == firstTimeError) 
            return firstTimeError;
        
        _firstTimeError = firstTimeError;
        await SendErrorsChanged();
        return firstTimeError;
    }
}