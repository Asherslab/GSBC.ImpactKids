namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.People;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class CreatePersonRequest
{
    public string  FirstName     { get; set; } = null!;
    public string  LastName      { get; set; } = null!;
    public string? PreferredName { get; set; }
}