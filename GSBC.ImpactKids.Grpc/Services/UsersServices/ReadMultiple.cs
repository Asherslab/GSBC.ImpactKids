using GSBC.ImpactKids.Grpc.Data.Models;
using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Services.UsersServices;

public partial class UsersService
{
    public async Task<BasicReadMultipleResponse<User>?> ReadMultiple(
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

        List<DbUser> users = await query.ToListAsync(token);

        return new BasicReadMultipleResponse<User>
        {
            Success = true,
            Entities = users.Select(converter.Convert).ToList()
        };
    }
}