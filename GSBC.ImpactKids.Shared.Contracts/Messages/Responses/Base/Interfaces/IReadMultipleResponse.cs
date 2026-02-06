using System.Collections.Immutable;

namespace GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base.Interfaces;

public interface IReadMultipleResponse<T> : ISuccessResponse, IErrorResponse
{
    public ImmutableList<T>   Entities   { get; init; }
    public PaginationResponse Pagination { get; init; }
}