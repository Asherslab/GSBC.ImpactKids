using GSBC.ImpactKids.Grpc.Data.Models.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

namespace GSBC.ImpactKids.Grpc.Features.Scheduling.ScheduleServices;

public partial class ScheduleService
{
    public Task<BasicResponse> AddGroup(AddScheduleGroupRequest request, CallContext context = default)
    {
        string name = request.Name.Trim();

        if (name.Length == 0)
            return Task.FromResult(BasicResponse.WithError(ScheduleGroupNameNull));

        return EditAsync(request.ScheduleId, (schedule, _) =>
        {
            // Names are what the label feed is asked for (?group=4-6), so two the same would be ambiguous.
            if (schedule.Groups.Any(g => string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase)))
                return BasicResponse.WithError(ScheduleGroupNameTaken);

            DbScheduleGroup group = new()
            {
                Id = Guid.NewGuid(),
                ScheduleId = schedule.Id,
                Name = name,
                Position = schedule.Groups.Count
            };

            db.ScheduleGroups.Add(group);
            schedule.Groups.Add(group);
            return null;
        }, context.ServerCallContext, context.CancellationToken);
    }
}
