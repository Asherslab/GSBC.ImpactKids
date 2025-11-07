namespace GSBC.ImpactKids.Shared.Contracts.Entities.People;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class MedicalType
{
    public required Guid   Id    { get; set; }
    public required string Label { get; set; }
}