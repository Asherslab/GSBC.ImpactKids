using Riok.Mapperly.Abstractions;

namespace GSBC.ImpactKids.Grpc.Data.Models.People;

public class DbMedicalNote
{
    public required Guid Id { get; set; }

    public required Guid? MedicalTypeId { get; set; }

    [MapperIgnore]
    public DbMedicalType? MedicalType { get; set; }

    public string? Notes  { get; set; }
    public bool    Severe { get; set; }

    public required Guid PersonId { get; set; }

    [MapperIgnore]
    public DbPerson? Person { get; set; }
}