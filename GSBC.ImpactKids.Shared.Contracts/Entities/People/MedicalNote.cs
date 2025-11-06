namespace GSBC.ImpactKids.Shared.Contracts.Entities.People;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class MedicalNote
{
    public Guid Id { get; set; }
    
    public required Guid?  MedicalTypeId { get; set; }
    public required string MedicalType   { get; set; }

    public string? Notes  { get; set; }
    public bool    Severe { get; set; }
}