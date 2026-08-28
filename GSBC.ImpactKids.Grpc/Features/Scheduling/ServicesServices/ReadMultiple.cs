using GSBC.ImpactKids.Grpc.Data.Models.Scheduling;
using GSBC.ImpactKids.Grpc.Extensions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

namespace GSBC.ImpactKids.Grpc.Features.Scheduling.ServicesServices;

public partial class ServicesService
{
    /// <summary>
    /// Open to wall displays as well as leaders. A display with no service id in its url
    /// picks today's service out of this list itself, which is the logic that used to live
    /// server side in the deleted pickup display service.
    /// <para>
    /// Opened to displays in <c>Program.cs</c>, not by an attribute here - see
    /// <see cref="Features.Authentication.DisplayAuth.DisplayEndpointExtensions"/> for why an
    /// attribute on this method would be silently ignored.
    /// </para>
    /// </summary>
    public async IAsyncEnumerable<BasicReadMultipleResponse<Service>> BasicReadMultiple(
        BasicReadMultipleRequest request,
        CallContext              context = default
    )
    {
        CancellationToken token = context.CancellationToken;

        IQueryable<DbService> query = db.Services;

        if (request.SearchString != null)
        {
            // ReSharper disable once SpecifyACultureInStringConversionExplicitly
            query = query.Where(x =>
                x.Name!.ToLower().Contains(request.SearchString.ToLower()) ||
                x.Date.ToString().ToLower().Contains(request.SearchString.ToLower())
            );
        }

        query = query.OrderBy(x => x.Date);

        query = query.Paginate(request);

        await foreach (BasicReadMultipleResponse<Service> response in query.ReturnInBatches(converter, token: token))
        {
            yield return response;
        }
    }
}