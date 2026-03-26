using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Elvanto;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Elvanto;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace GSBC.ImpactKids.WASM.Components.Common;

public partial class Roster : ComponentBase
{
    [Parameter]
    public Rosters Rosters { get; set; } = Rosters.ImpactKids;

    [Parameter]
    public DateTime? StartDate { get; set; }

    [Parameter]
    public DateTime? EndDate { get; set; }

    [Parameter]
    public Breakpoint MobileLayoutBreakpoint { get; set; } = Breakpoint.Xs;

    private static string TitleForService(string service)
    {
        DateTime date = DateTime.Parse(service).ToLocalTime();

        return $"{DateTime.Parse(service):dd/MM} {date.DayOfWeek.ToString()[..3]} {date.ToString("tt").ToUpper()}";
    }

    private static string PositionForService(ElvantoServicePosition position, string service)
    {
        return position.PositionsForService.GetValueOrDefault(service, "");
    }

    private ElvantoServicePositionsResponse? _elvantoResponse;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        _elvantoResponse = await ElvantoService.GetServicePositions(new ServicePositionsRequest
        {
            Rosters = Rosters,
            StartDate = StartDate,
            EndDate = EndDate
        });
    }
}