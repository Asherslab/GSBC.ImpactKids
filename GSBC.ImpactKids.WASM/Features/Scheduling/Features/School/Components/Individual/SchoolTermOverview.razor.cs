using GSBC.ImpactKids.WASM.Components.Common.Inputs;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace GSBC.ImpactKids.WASM.Features.Scheduling.Features.School.Components.Individual;

public partial class SchoolTermOverview : ComponentBase
{
    [Parameter]
    public required Guid? SchoolTermId { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        SchoolTermsStore.Subscribe(_ =>
            {
                if (_detailsSent || _detailsState != ModificationState.Updating) return;

                Snackbar.Add(
                    "Somebody else has made modifications to this service, your edit has been cancelled",
                    Severity.Warning,
                    x =>
                    {
                        x.CloseAfterNavigation = true;
                        x.VisibleStateDuration = int.MaxValue;
                    });
                _detailsState = ModificationState.Reading;
                StateHasChanged();
            }
        );
    }

    private bool               _detailsSent;
    private ModificationState  _detailsState = ModificationState.Reading;
    private SchoolTermDetails? _schoolTermDetailsComponent;

    private async Task UpdateService()
    {
        if (_detailsState == ModificationState.Updating && _schoolTermDetailsComponent != null)
        {
            _detailsSent = true;
            try
            {
                bool success = await _schoolTermDetailsComponent.UpdateSchoolTerm();
                if (success)
                    _detailsState = ModificationState.Reading;
            }
            finally
            {
                _detailsSent = false;
            }
        }
    }

    private async Task DeleteService()
    {
        if (_schoolTermDetailsComponent != null)
        {
            await _schoolTermDetailsComponent.DeleteSchoolTerm();
        }
    }
}