using GSBC.ImpactKids.Shared.Contracts.Entities.Pagination;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base.Interfaces;

namespace GSBC.ImpactKids.Grpc.Extensions;

public static class QueryableExtensions
{
    public static IQueryable<T> Paginate<T>(this IQueryable<T> query, IReadMultipleRequest request)
    {
        request.Pagination ??= new PaginationRequest();
        if (!request.Pagination.Disabled)
        {
            query = query
                .Skip(request.Pagination.Page * request.Pagination.PerPage)
                .Take(request.Pagination.PerPage);
        }
        return query;
    }
}