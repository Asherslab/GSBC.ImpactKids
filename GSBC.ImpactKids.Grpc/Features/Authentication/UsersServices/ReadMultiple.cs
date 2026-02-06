using GSBC.ImpactKids.Grpc.Data.Models;
using GSBC.ImpactKids.Grpc.Extensions;
using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

namespace GSBC.ImpactKids.Grpc.Features.Authentication.UsersServices;

public partial class UsersService
{
    public async IAsyncEnumerable<BasicReadMultipleResponse<User>> BasicReadMultiple(
        BasicReadMultipleRequest request,
        CallContext              context = default
    )
    {
        CancellationToken token = context.CancellationToken;

        IQueryable<DbUser> query = db.Users;

        if (request.SearchString != null)
        {
            query = query.Where(x => x.Name.ToLower().Contains(request.SearchString.ToLower()));
        }

        query = query.OrderBy(x => x.Name);
        
        query = query.Paginate(request);
        
        await foreach (BasicReadMultipleResponse<User> response in query.ReturnInBatches(converter, token: token))
        {
            yield return response;
        }
    }
}