using System.ComponentModel.DataAnnotations.Schema;

namespace GSBC.ImpactKids.Grpc.Data.Models;

public class DbSchoolTerm
{
    public required Guid   Id   { get; set; }
    public required string Name { get; set; }

    [Column(TypeName="date")]
    public required DateTime StartDate { get; set; }
    [Column(TypeName="date")]
    public required DateTime EndDate   { get; set; }

    public List<DbService> Services { get; set; } = [];
}