namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class MetabaseJwtRequest
{
    public required int    DashboardId { get; set; }
    public required string Type        { get; set; }
}