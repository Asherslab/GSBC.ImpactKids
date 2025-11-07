namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.People.Allergies;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class CreateAllergyRequest
{
    public Guid PersonId { get; set; }

    public Guid? AllergenId { get; set; }

    public string? Notes  { get; set; }
    public bool    Severe { get; set; }
}