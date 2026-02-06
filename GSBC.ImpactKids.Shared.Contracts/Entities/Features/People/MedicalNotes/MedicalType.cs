namespace GSBC.ImpactKids.Shared.Contracts.Entities.Features.People.MedicalNotes;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public record MedicalType : IIdentifiable
{
    public required Guid   Id    { get; init; }
    public required string Label { get; init; }
}