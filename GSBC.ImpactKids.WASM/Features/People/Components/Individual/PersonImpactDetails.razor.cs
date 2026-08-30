using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.People.Components.Individual;

public partial class PersonImpactDetails
{
    [Parameter]
    public Func<bool, Task>? ErrorsChanged { get; set; }
    
    private bool? _mediaConsentError;
    private bool? _firstTimeError;
    private bool? _genderError;
    
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
            _firstTimeError == null ||
            _genderError == null)
            return;

        bool error = _mediaConsentError.Value ||
                     _firstTimeError.Value ||
                     _genderError.Value;
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
    
    /// <summary>
    /// Null is the error, and it is the point of the field: a child cannot be signed in against a
    /// profile that does not say. Expect this to fire on roughly a third of children the first
    /// night, because that is how many Elvanto has no gender for.
    /// </summary>
    private async Task<bool> GenderGetError(Gender? gender)
    {
        bool genderError = gender == null;
        if (_genderError == genderError)
            return genderError;

        _genderError = genderError;
        await SendErrorsChanged();
        return genderError;
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