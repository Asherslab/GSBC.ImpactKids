using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base.Interfaces;

namespace GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Analytics;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class MemoryVerseAnalyticsResponse : ISuccessResponse, IErrorResponse
{
    public List<string> XAxisLabels { get; set; } = [];

    public List<MemoryVerseVerticalAxis> VerticalAxis { get; set; } = [];

    public required bool    Success { get; set; }
    public          string? Error   { get; set; }
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class MemoryVerseVerticalAxis
{
    public required string Label { get; set; }

    public required double[] DataPoints { get; set; }
}