using System.Collections.Immutable;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;

namespace GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base.Interfaces;

public interface IReadMultipleResponse<T> : ISuccessResponse, IErrorResponse
{
    public ImmutableList<T>   Entities   { get; init; }
    public PaginationResponse Pagination { get; init; }
}