using GSBC.ImpactKids.Grpc.Data.Models.Attendance;
using GSBC.ImpactKids.Grpc.Extensions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Attendance;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

namespace GSBC.ImpactKids.Grpc.Features.Attendance.AttendanceRecordServices;

public partial class AttendanceRecordService
{
    /// <summary>
    /// Open to wall displays as well as leaders - the pickup wall works out who is waiting
    /// from <see cref="AttendanceRecord.AwaitingPickup"/> over these rows. Read only: a
    /// display that tried to sign a child out would be refused by the policy on
    /// <c>Update</c>, and refused again at the database by
    /// <see cref="Data.Interceptors.DisplayReadOnlyInterceptor"/>.
    /// <para>
    /// Opened to displays in <c>Program.cs</c>, not by an attribute here - see
    /// <see cref="Features.Authentication.DisplayAuth.DisplayEndpointExtensions"/> for why an
    /// attribute on this method would be silently ignored.
    /// </para>
    /// </summary>
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