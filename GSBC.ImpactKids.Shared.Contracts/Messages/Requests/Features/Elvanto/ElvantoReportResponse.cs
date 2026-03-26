namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Elvanto;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class ElvantoReportResponse
{
    public required string Label   { get; set; }
    public required string Id      { get; set; }
    public required string AuthKey { get; set; }
}