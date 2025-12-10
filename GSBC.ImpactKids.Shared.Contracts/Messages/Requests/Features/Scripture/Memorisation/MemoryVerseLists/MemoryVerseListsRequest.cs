namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scripture.Memorisation.MemoryVerseLists;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class MemoryVerseListsRequest : IReadMultipleRequest
{
    public PaginationRequest? Pagination   { get; set; }
    public string?            SearchString { get; set; }

    public Guid? SchoolTermId { get; set; }
}