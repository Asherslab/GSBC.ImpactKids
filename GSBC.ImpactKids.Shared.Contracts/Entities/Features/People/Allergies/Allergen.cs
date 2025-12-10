namespace GSBC.ImpactKids.Shared.Contracts.Entities.Features.People.Allergies;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class Allergen
{
    public required Guid   Id    { get; set; }
    public required string Label { get; set; }
}