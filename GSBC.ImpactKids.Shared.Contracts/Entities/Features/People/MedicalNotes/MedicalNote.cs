namespace GSBC.ImpactKids.Shared.Contracts.Entities.Features.People.MedicalNotes;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class MedicalNote
{
    public Guid Id { get; set; }
    
    public required Guid?  MedicalTypeId { get; set; }
    public required string MedicalType   { get; set; }

    public string? Notes  { get; set; }
    public bool    Severe { get; set; }
}