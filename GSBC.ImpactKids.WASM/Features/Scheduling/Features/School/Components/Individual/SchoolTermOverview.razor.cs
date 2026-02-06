using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.Scheduling.Features.School.Components.Individual;

public partial class SchoolTermOverview
{
    [Parameter]
    public required Guid? Id { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
    }
}