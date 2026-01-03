namespace GSBC.ImpactKids.Shared.Contracts.Entities.Features.People;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public record SchoolGrade : IIdentifiable
{
    public required Guid Id          { get; init; }
    public          int  OrderNumber { get; init; }

    public required string  Label     { get; init; }
    public required string? ElvantoId { get; init; }

    public Guid? NextGradeId   { get; init; }
    public Guid? PreviousGrade { get; init; }
}