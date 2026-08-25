using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base.Interfaces;

namespace GSBC.ImpactKids.Shared.Contracts.Messages.Responses.People;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class SyncResponse : ISuccessResponse, IErrorResponse
{
    public required bool    Success             { get; init; }
    public          string? Error               { get; init; }
    public required string  OperationId         { get; init; }
    public required string  Mode                { get; init; }
    public required int     PeopleProcessed     { get; init; }
    public required int     InboundPeople       { get; init; }
    public required int     InboundFields       { get; init; }
    public required int     OutboundPeople      { get; init; }
    public required int     OutboundFields      { get; init; }
    public required int     Conflicts           { get; init; }
    public required int     AutoLinked          { get; init; }
    public required int     ManualReviewQueued  { get; init; }
    public required int     Archived            { get; init; }
    public required int     Diverged            { get; init; }
    public List<SyncManualReviewItem> ManualReviewItems { get; init; } = [];
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class SyncManualReviewItem
{
    public required string PersonId        { get; init; }
    public required string ElvantoId       { get; init; }
    public required string Reason          { get; init; }
    public required int    MatchConfidence { get; init; }
}
