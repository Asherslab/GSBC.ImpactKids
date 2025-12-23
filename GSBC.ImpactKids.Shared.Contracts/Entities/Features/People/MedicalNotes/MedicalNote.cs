namespace GSBC.ImpactKids.Shared.Contracts.Entities.Features.People.MedicalNotes;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public record MedicalNote
{
    public Guid Id { get; init; }
    
    public required Guid?  MedicalTypeId { get; init; }
    public required string MedicalType   { get; init; }

    public string? Notes  { get; init; }
    public bool    Severe { get; init; }
}