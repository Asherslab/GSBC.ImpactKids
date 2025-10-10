using GSBC.ImpactKids.Shared.Contracts.Entities.Pagination;

namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base.Interfaces;

public interface IReadMultipleRequest
{
    public PaginationRequest? Pagination   { get; set; }
    public string?            SearchString { get; set; }
}