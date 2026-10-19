using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.Schedules;

namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.Schedules;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class UpdateScheduleItemTemplateRequest
    : ReadRequestBase, IUpdateRequest<ScheduleItemTemplate, UpdateScheduleItemTemplateRequest>
{
    public override string Id { get; set; } = null!;

    public DeltaUpdate<string>    Name        { get; set; } = new();
    public DeltaUpdate<int>       Minutes     { get; set; } = new();
    public DeltaUpdate<TimeSpan?> StartsAt    { get; set; } = new();
    public DeltaUpdate<bool>      NeedsFillIn { get; set; } = new();

    public static UpdateScheduleItemTemplateRequest FromEntity(ScheduleItemTemplate entity)
    {
        UpdateScheduleItemTemplateRequest request = new()
        {
            Guid = entity.Id
        };

        request.Name.SetInitialValue(entity.Name);
        request.Minutes.SetInitialValue(entity.Minutes);
        request.StartsAt.SetInitialValue(entity.StartsAt);
        request.NeedsFillIn.SetInitialValue(entity.NeedsFillIn);
        return request;
    }
}
