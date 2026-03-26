using GSBC.ImpactKids.Grpc.Data.Models.Attendance;
using GSBC.ImpactKids.Grpc.Extensions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Attendance;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

namespace GSBC.ImpactKids.Grpc.Features.Attendance.AttendanceItemTypeServices;

public partial class AttendanceItemTypeService
{
    public async IAsyncEnumerable<BasicReadMultipleResponse<AttendanceItemType>> BasicReadMultiple(
        BasicReadMultipleRequest request,
        CallContext              context = default
    )
    {
        CancellationToken token = context.CancellationToken;

        IQueryable<DbAttendanceItemType> query = db.AttendanceItemTypes;

        if (request.SearchString != null)
        {
            foreach (string search in request.SearchString.Split(" "))
            {
                query = query.Where(x =>
                    x.Label.ToLower().Contains(search.ToLower())
                );
            }
        }

        query = query
            .OrderBy(x => x.Reward)
            .ThenBy(x => x.Label);

        query = query.Paginate(request);

        await foreach (BasicReadMultipleResponse<AttendanceItemType> response in query.ReturnInBatches(converter,
                           token: token))
        {
            yield return response;
        }
    }
}