using GSBC.ImpactKids.Shared.Contracts.Entities.Pagination;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base.Interfaces;

namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.MemoryVerseLists;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class MemoryVerseListsRequest : IReadMultipleRequest
{
    public PaginationRequest? Pagination   { get; set; }
    public string?            SearchString { get; set; }

    public Guid? SchoolTermId { get; set; }
}