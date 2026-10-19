using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Services.Base;

namespace GSBC.ImpactKids.Shared.Contracts.Services.Features.Scheduling;

/// <summary>
/// Schedule templates and services' schedules alike. <see cref="ICreateService{T}.Create"/> makes a
/// template; a service gets its schedule from <see cref="ApplyTemplate"/>, or its service type's
/// default when the service is created.
/// </summary>
[Service("gRPC/GSBC.ImpactKids.Schedules")]
public interface IScheduleService
    : IBasicReadMultipleService<Schedule>,
        ICreateService<CreateScheduleTemplateRequest>,
        IUpdateService<UpdateScheduleTemplateRequest>,
        IBasicDeleteService<Schedule>
{
    Task<BasicReadResponse<Guid?>> ApplyTemplate(ApplyScheduleTemplateRequest request, CallContext context = default);

    Task<BasicResponse> AddGroup(AddScheduleGroupRequest       request, CallContext context = default);
    Task<BasicResponse> RenameGroup(RenameScheduleGroupRequest request, CallContext context = default);
    Task<BasicResponse> RemoveGroup(BasicReadRequest           request, CallContext context = default);

    Task<BasicResponse> AddBlock(AddScheduleBlockRequest   request, CallContext context = default);
    Task<BasicResponse> MoveBlock(MoveScheduleBlockRequest request, CallContext context = default);
    Task<BasicResponse> RemoveBlock(BasicReadRequest       request, CallContext context = default);

    Task<BasicResponse> AddLane(BasicReadRequest                   request, CallContext context = default);
    Task<BasicResponse> SetLaneGroups(SetScheduleLaneGroupsRequest request, CallContext context = default);
    Task<BasicResponse> RemoveLane(BasicReadRequest                request, CallContext context = default);

    Task<BasicResponse> AddItem(AddScheduleItemRequest       request, CallContext context = default);
    Task<BasicResponse> UpdateItem(UpdateScheduleItemRequest request, CallContext context = default);
    Task<BasicResponse> MoveItem(MoveScheduleItemRequest     request, CallContext context = default);
    Task<BasicResponse> AdjustItem(AdjustScheduleItemRequest request, CallContext context = default);
    Task<BasicResponse> RemoveItem(BasicReadRequest          request, CallContext context = default);
}
