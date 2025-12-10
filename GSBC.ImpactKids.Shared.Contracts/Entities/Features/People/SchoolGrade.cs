namespace GSBC.ImpactKids.Shared.Contracts.Entities.Features.People;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class SchoolGrade
{
    public required Guid Id          { get; set; }
    public          int  OrderNumber { get; set; }

    public required string  Label     { get; set; }
    public required string? ElvantoId { get; set; }

    public Guid? NextGradeId   { get; set; }
    public Guid? PreviousGrade { get; set; }
}