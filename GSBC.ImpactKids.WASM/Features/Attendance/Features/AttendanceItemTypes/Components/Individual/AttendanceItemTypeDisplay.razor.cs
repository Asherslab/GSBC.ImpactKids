using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Attendance;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using MudBlazor.Utilities;

namespace GSBC.ImpactKids.WASM.Features.Attendance.Features.AttendanceItemTypes.Components.Individual;

public partial class AttendanceItemTypeDisplay
{
    [Parameter]
    public string? Link { get; set; }

    [Parameter]
    public EventCallback OnClick { get; set; }

    [Parameter]
    public RenderFragment<AttendanceItemType?>? SuffixContent { get; set; }

    [Parameter]
    public bool Other { get; set; }

    private string? Href => Link;

    private string Class => CssBuilder.Empty()
        .AddClass("clickable mud-ripple", Href != null || OnClick.HasDelegate)
        .AddClass("d-flex justify-start flex-direction-row flex-grow-0")
        .Build();

    private string? AvatarDisplay =>
        IconsForLabels.GetValueOrDefault(DisplayText, Icons.Material.Filled.QuestionMark);

    private Color AvatarColor =>
        Entity.Data == null
            ? Other
                ? Color.Primary
                : Color.Default
            : Entity.Data.Reward != null
                ? Color.Success
                : Color.Primary;

    private string DisplayText =>
        Other
            ? "Other"
            : Entity.Data?.Label ?? "Attendance Item";

    protected override async Task OnInitializedAsync()
    {
        await Task.WhenAll(
            EntityStore.RefreshAll()
        );
    }

    public static readonly Dictionary<string, string?> IconsForLabels = new()
    {
        { "Other", Icons.Material.Filled.Notes },
        { "Came Early", Icons.Material.Filled.PunchClock },
        { "Bible", Icons.Material.Filled.Book },
        { "Phone", Icons.Material.Filled.Smartphone }
    };
}