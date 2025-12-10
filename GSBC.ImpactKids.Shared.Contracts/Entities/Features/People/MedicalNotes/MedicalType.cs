namespace GSBC.ImpactKids.Shared.Contracts.Entities.Features.People.MedicalNotes;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class MedicalType
{
    public required Guid   Id    { get; set; }
    public required string Label { get; set; }
}