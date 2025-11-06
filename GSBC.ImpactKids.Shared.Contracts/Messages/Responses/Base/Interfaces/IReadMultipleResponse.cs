namespace GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base.Interfaces;

public interface IReadMultipleResponse<T> : ISuccessResponse, IErrorResponse
{
    public ICollection<T>     Entities   { get; set; }
    public PaginationResponse Pagination { get; set; }
}