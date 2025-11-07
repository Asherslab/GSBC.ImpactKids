namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.MemorisationEntries;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class MemorisationEntriesRequest : IReadMultipleRequest
{
    public PaginationRequest? Pagination   { get; set; }
    public string?            SearchString { get; set; }

    public bool IncludePerson      { get; set; }
    public bool IncludeService     { get; set; }
    public bool IncludeMemoryVerse { get; set; }

    public Guid? PersonId      { get; set; }
    public Guid? ServiceId     { get; set; }
    public Guid? SchoolTermId  { get; set; }
    public Guid? MemoryVerseId { get; set; }

    public bool CurrentSchoolTerm { get; set; }
}