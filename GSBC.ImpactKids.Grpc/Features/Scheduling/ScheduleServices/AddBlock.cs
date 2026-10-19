using GSBC.ImpactKids.Grpc.Data.Models.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

namespace GSBC.ImpactKids.Grpc.Features.Scheduling.ScheduleServices;

public partial class ScheduleService
{
    /// <summary>
    /// An empty together block (one lane) or split (several). A new split's lanes are given the
    /// schedule's groups one each, in order, while there are groups to go round - the common case of
    /// "split into our groups" needs no further setting up.
    /// </summary>
    public Task<BasicResponse> AddBlock(AddScheduleBlockRequest request, CallContext context = default)
    {
        if (request.Lanes is < 1 or > ScheduleRules.MaxLanes)
            return Task.FromResult(BasicResponse.WithError(ScheduleBlockLanesInvalid));

        return EditAsync(request.ScheduleId, (schedule, _) =>
        {
            DbScheduleBlock block = new()
            {
                Id = Guid.NewGuid(),
                ScheduleId = schedule.Id,
                Position = 0
            };

            for (int l = 0; l < request.Lanes; l++)
            {
                DbScheduleLane lane = NewLane(block.Id, l);

                if (request.Lanes > 1 && l < schedule.Groups.Count)
                    lane.GroupIds = [schedule.Groups[l].Id];

                db.ScheduleLanes.Add(lane);
                block.Lanes.Add(lane);
            }

            db.ScheduleBlocks.Add(block);
            schedule.Blocks.Insert(Math.Clamp(request.Position, 0, schedule.Blocks.Count), block);
            return null;
        }, context.ServerCallContext, context.CancellationToken);
    }
}
