namespace GSBC.ImpactKids.Shared.Contracts.Entities.Features.People.Allergies;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class Allergy
{
    public Guid Id { get; set; }
    
    public required Guid?  AllergenId { get; set; }
    public required string Allergen   { get; set; }

    public string? Notes  { get; set; }
    public bool    Severe { get; set; }
}