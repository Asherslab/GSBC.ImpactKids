using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Attendance;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Attendance.AttendanceItemRecords;
using GSBC.ImpactKids.WASM.Components.Common.Inputs;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.Attendance.Features.AttendanceItemRecords.Components.Individual;

public partial class AttendanceItemRecordDetails
{
    [Parameter]
    public Guid? AttendanceRecordId { get; set; }

    [Parameter]
    public Guid? AttendanceItemTypeId { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        if (AttendanceItemTypeId != null)
            CreateRequest.AttendanceItemTypeId = AttendanceItemTypeId.Value;

        HandleStateChangeSubscriptionDisposal(AttendanceItemTypesStore);

        await Task.WhenAll(
            EntityStore.RefreshAll(),
            AttendanceItemTypesStore.RefreshAll()
        );
    }

    protected override void OnRetrievedEntity()
    {
        base.OnRetrievedEntity();
        CreateRequest.AttendanceItemTypeId = Entity.Data?.AttendanceItemTypeId;
    }

    protected override CreateAttendanceItemRecordRequest ModifyCreateRequest(CreateAttendanceItemRecordRequest request)
    {
        if (AttendanceRecordId != null)
            request.AttendanceRecordId = AttendanceRecordId.Value;
        if (CreateRequest.AttendanceItemTypeId != null && GetItemType()?.RequiresReturning == true)
            request.ItemReturned = false;
        return request;
    }
    
    private AttendanceItemType? GetItemType()
        => AttendanceItemTypesStore.GetState()
            .First(x => x.Id == CreateRequest.AttendanceItemTypeId).Data;
    
    private bool DisableItemReturned()
        => State != ModificationState.Updating &&
           CreateRequest.AttendanceItemTypeId != null;
    
    private string GetNullItemReturnedText()
        => CreateRequest.AttendanceItemTypeId == null && State == ModificationState.Creating
            ? "Does not require returning"
            : "Please Select";
    
    private bool DisableYesItemReturned()
        => CreateRequest.AttendanceItemTypeId == null &&
           State == ModificationState.Creating;
    
    private string GetNoItemReturnedText()
        => CreateRequest.AttendanceItemTypeId == null && State == ModificationState.Creating
            ? "Requires Returning"
            : "No";
}