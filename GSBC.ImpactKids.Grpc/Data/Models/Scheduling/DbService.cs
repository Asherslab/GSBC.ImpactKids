using System.ComponentModel.DataAnnotations.Schema;
using GSBC.ImpactKids.Grpc.Data.Models.MemoryVerses;
using GSBC.ImpactKids.Grpc.Data.Models.Scheduling.School;
using Riok.Mapperly.Abstractions;

namespace GSBC.ImpactKids.Grpc.Data.Models.Scheduling;

public class DbService
{
    public required Guid    Id   { get; set; }
    public          string? Name { get; set; }

    public required DateTimeOffset Date { get; set; }

    [MapperIgnore]
    public Guid? SchoolTermId { get; set; }

    public DbSchoolTerm? SchoolTerm { get; set; }

    [MapperIgnore]
    public Guid? ServiceTypeId { get; set; }

    public DbServiceType? ServiceType { get; set; }

    [MapperIgnore]
    public DbDollarStoreEntry? DollarStoreEntry { get; set; }

    [MapperIgnore]
    public List<DbMemoryVerse> MemoryVerses { get; set; } = [];
}