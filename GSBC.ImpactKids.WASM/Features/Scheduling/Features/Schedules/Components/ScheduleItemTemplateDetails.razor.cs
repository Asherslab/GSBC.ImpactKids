using GSBC.ImpactKids.WASM.Components.Common.Inputs;

namespace GSBC.ImpactKids.WASM.Features.Scheduling.Features.Schedules.Components;

public partial class ScheduleItemTemplateDetails
{
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        await EntityStore.RefreshAll();
    }

    // There is no CreateOrUpdate wrapper for a time picker, and one field does not justify one.
    private TimeSpan? StartsAt => State == ModificationState.Creating
        ? CreateRequest.StartsAt
        : UpdateRequest.StartsAt.Value;

    private void SetStartsAt(TimeSpan? value)
    {
        if (State == ModificationState.Creating)
            CreateRequest.StartsAt = value;
        else if (State == ModificationState.Updating)
            UpdateRequest.StartsAt.Value = value;
    }
}
