namespace GSBC.ImpactKids.Grpc.Data.Models.People;

public class DbSchoolGrade
{
    public required Guid Id          { get; set; }
    public          int  OrderNumber { get; set; }

    public required string  Label     { get; set; }
    public required string? ElvantoId { get; set; }

    public Guid? NextGradeId   { get; set; }
    public Guid? PreviousGrade { get; set; }
}