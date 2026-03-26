namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Authentication.Login;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class CreateSelfRequest
{
    public required string GoogleSub { get; set; }
    public required string Name      { get; set; }
}