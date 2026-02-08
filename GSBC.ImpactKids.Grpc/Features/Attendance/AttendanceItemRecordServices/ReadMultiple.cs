using GSBC.ImpactKids.Grpc.Data.Models.Attendance;
using GSBC.ImpactKids.Grpc.Extensions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Attendance;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

namespace GSBC.ImpactKids.Grpc.Features.Attendance.AttendanceItemRecordServices;

public partial class AttendanceItemRecordService
{
    public async IAsyncEnumerable<BasicReadMultipleResponse<AttendanceItemRecord>> BasicReadMultiple(
        BasicReadMultipleRequest request,
        CallContext              context = default
    )
    {
        CancellationToken token = context.CancellationToken;

        IQueryable<DbAttendanceItemRecord> query = db.AttendanceItemRecords;

        if (request.SearchString != null)
        {
            foreach (string search in request.SearchString.Split(" "))
            {
                query = query.Where(x =>
                    x.AttendanceRecord!.Person!.FirstName.ToLower().Contains(search.ToLower()) ||
                    x.AttendanceRecord!.Person!.LastName.ToLower().Contains(search.ToLower())
                );
            }
        }

        query = query
            .OrderBy(x => x.AttendanceItemType!.Reward)
            .ThenBy(x => x.AttendanceItemType!.Label);

        query = query.Paginate(request);

        await foreach (BasicReadMultipleResponse<AttendanceItemRecord> response in query.ReturnInBatches(converter,
                           token: token))
        {
            yield return response;
        }
    }
}