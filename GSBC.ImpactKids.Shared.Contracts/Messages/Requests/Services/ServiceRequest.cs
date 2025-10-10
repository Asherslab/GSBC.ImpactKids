namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Services;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class ServiceByDateRequest
{
    public required DateTime Date { get; set; }
}