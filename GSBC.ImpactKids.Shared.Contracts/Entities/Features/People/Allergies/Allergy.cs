namespace GSBC.ImpactKids.Shared.Contracts.Entities.Features.People.Allergies;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public record Allergy
{
    public Guid Id { get; init; }

    public required Guid PersonId { get; init; }

    public required Guid? AllergenId { get; init; }

    public string? Notes  { get; init; }
    public bool    Severe { get; init; }
}