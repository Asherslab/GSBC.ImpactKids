using System.Collections.Immutable;
using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Attendance;
using GSBC.ImpactKids.WASM.Extensions;
using GSBC.ImpactKids.WASM.Features.Attendance.Features.AttendanceItemTypes.Components.Individual;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using MudBlazor.Utilities;

namespace GSBC.ImpactKids.WASM.Features.Attendance.Features.AttendanceItemRecords.Components.Individual;

public partial class AttendanceItemRecordDisplay
{
    [Parameter]
    public string? Link { get; set; }

    [Parameter]
    public EventCallback OnClick { get; set; }

    [Parameter]
    public RenderFragment<AttendanceItemRecord?>? SuffixContent { get; set; }
    
    [Parameter]
    public bool ShowReturnError { get; set; }
    
    private string? Href => Link;
    
    private string Css => CssBuilder.Empty()
        .AddClass("clickable mud-ripple", Href != null || OnClick.HasDelegate)
        .AddClass("error-highlight", RequiresReturning() && Entity.Data?.ItemReturned != true && ShowReturnError)
        .AddClass("d-flex justify-start flex-direction-row flex-grow-0")
        .Build();

    private AsyncData<AttendanceItemType> _attendanceItemType = AsyncData<AttendanceItemType>.NotAsked();

    private bool RequiresReturning() => _attendanceItemType.Data?.RequiresReturning == true ||
                                        _attendanceItemType.Data == null && Entity.Data?.ItemReturned != null;
    
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        await Task.WhenAll(
            EntityStore.RefreshAll(),
            AttendanceItemTypesStore.RefreshAll()
        );
    }

    private string? _avatarDisplay;
    private Color   _avatarColor = Color.Default;
    private string  _displayText = "Allergies not requested";

    protected override void OnRetrievedEntity()
    {
        AsyncData<ImmutableList<AttendanceItemType>> attendanceItemTypes = AttendanceItemTypesStore.GetState().Entities;

        if (!attendanceItemTypes.HasData)
        {
            _attendanceItemType = _attendanceItemType.CopyStatus(attendanceItemTypes);
            StateHasChanged();
            return;
        }

        _avatarDisplay = "N";

        AttendanceItemType? attendanceItemType = attendanceItemTypes.Data!
            .FirstOrDefault(x => x.Id == Entity.Data!.AttendanceItemTypeId);
        

        _attendanceItemType = attendanceItemType == null
            ? _attendanceItemType.ToFailure("Failed to find Item Type")
            : _attendanceItemType.ToSuccess(attendanceItemType);

        string itemTypeLabel = attendanceItemType?.Label ?? "Other";
        
        _avatarDisplay = AttendanceItemTypeDisplay.IconsForLabels.GetValueOrDefault(itemTypeLabel, Icons.Material.Filled.QuestionMark);

        _displayText = itemTypeLabel;

        _avatarColor = attendanceItemType == null
            ? Color.Primary
            : attendanceItemType.Reward != null
                ? Color.Success
                : Color.Primary;
    }

    // private async Task OnUpdate() =>
    //     await DetailsComponentDialog.Open<AllergyDetails>(
    //         DialogService,
    //         "Update Allergy",
    //         ModificationState.Updating,
    //         Id
    //     );
    //
    // private async Task OnDelete() =>
    //     await DeleteWithDialog<AttendanceItemRecord>(
    //         AttendanceItemRecordService,
    //         Entity.Data?.Id,
    //         () => Entity = Entity.ToLoading(),
    //         RetrieveEntity
    //     );
}