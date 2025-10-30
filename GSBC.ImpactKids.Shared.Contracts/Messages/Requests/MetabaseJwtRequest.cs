namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class MetabaseJwtRequest
{
    public int DashboardId { get; set; }
}