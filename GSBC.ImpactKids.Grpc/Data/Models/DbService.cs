using System.ComponentModel.DataAnnotations.Schema;
using Riok.Mapperly.Abstractions;

namespace GSBC.ImpactKids.Grpc.Data.Models;

public class DbService
{
    public required Guid    Id   { get; set; }
    public          string? Name { get; set; }

    [Column(TypeName="date")]
    public required DateTime Date { get; set; }

    public required Guid          SchoolTermId { get; set; }
    [MapperIgnore]
    public          DbSchoolTerm? SchoolTerm   { get; set; }
}