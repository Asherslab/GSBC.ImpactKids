using Riok.Mapperly.Abstractions;

namespace GSBC.ImpactKids.Grpc.Data.Models.Scheduling.School;

public class DbSchoolTerm
{
    public required Guid   Id   { get; set; }
    public required string Name { get; set; }

    public required DateTimeOffset StartDate { get; set; }
    public required DateTimeOffset EndDate   { get; set; }

    [MapperIgnore]
    public List<DbService> Services { get; set; } = [];
}