using GSBC.ImpactKids.Grpc.Data.Models.Attendance;
using GSBC.ImpactKids.Grpc.Extensions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Attendance;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

namespace GSBC.ImpactKids.Grpc.Features.Attendance.AttendanceRecordServices;

public partial class AttendanceRecordService
{
    public async IAsyncEnumerable<BasicReadMultipleResponse<AttendanceRecord>> BasicReadMultiple(
        BasicReadMultipleRequest request,
        CallContext              context = default
    )
    {
        CancellationToken token = context.CancellationToken;

        IQueryable<DbAttendanceRecord> query = db.AttendanceRecords;

        if (request.SearchString != null)
        {
            foreach (string search in request.SearchString.Split(" "))
            {
                query = query.Where(x =>
                    x.Person!.FirstName.ToLower().Contains(search.ToLower()) ||
                    x.Person.LastName.ToLower().Contains(search.ToLower())
                );
            }
        }

        query = query
            .OrderBy(x => x.SignedIn);

        query = query.Paginate(request);

        await foreach (BasicReadMultipleResponse<AttendanceRecord> response in query.ReturnInBatches(converter,
                           token: token))
        {
            yield return response;
        }
    }
}