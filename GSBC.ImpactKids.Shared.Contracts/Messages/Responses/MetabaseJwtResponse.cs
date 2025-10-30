namespace GSBC.ImpactKids.Shared.Contracts.Messages.Responses;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class MetabaseJwtResponse
{
    public required string? Jwt { get; set; }
}