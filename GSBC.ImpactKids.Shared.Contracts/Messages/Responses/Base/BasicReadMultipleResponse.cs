using GSBC.ImpactKids.Shared.Contracts.Entities.Pagination;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base.Interfaces;

namespace GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class BasicReadMultipleResponse<T> : IReadMultipleResponse<T>
{
    public ICollection<T>     Entities   { get; set; } = new List<T>();
    public PaginationResponse Pagination { get; set; } = PaginationResponse.Empty();

    public required bool    Success { get; set; }
    public          string? Error   { get; set; }
}