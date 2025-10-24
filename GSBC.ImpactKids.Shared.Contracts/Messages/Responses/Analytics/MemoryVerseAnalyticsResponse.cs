using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Entities.MemoryVerses;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base.Interfaces;

namespace GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Analytics;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class MemoryVerseAnalyticsResponse : ISuccessResponse, IErrorResponse
{
    // x axis
    public List<Service> Services { get; set; } = [];

    public List<MemoryVerseVerticalAxis> VerticalAxis { get; set; } = [];

    public required bool    Success { get; set; }
    public          string? Error   { get; set; }
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class MemoryVerseVerticalAxis
{
    public required MemoryVerse Verse { get; set; }

    public required double[] DataPoints { get; set; }
}