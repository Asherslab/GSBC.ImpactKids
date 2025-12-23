namespace GSBC.ImpactKids.Shared.Contracts.Entities.Features.People.Allergies;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public record Allergen
{
    public required Guid   Id    { get; init; }
    public required string Label { get; init; }
}