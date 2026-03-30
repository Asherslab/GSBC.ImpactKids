using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Data.Models.Scheduling;
using Riok.Mapperly.Abstractions;

namespace GSBC.ImpactKids.Grpc.Data.Models.Attendance;

public class DbAttendanceRecord
{
    public required Guid Id { get; set; }

    public required DateTimeOffset  SignedIn  { get; set; }
    public          DateTimeOffset? SignedOut { get; set; }

    public bool Deleted { get; set; }

    // Relationships \\

    public required Guid PersonId { get; set; }

    [MapperIgnore]
    public DbPerson? Person { get; set; }

    public required Guid SignedInUserId { get; set; }

    [MapperIgnore]
    public DbUser? SignedInUser { get; set; }

    public Guid? SignedOutUserId { get; set; }

    [MapperIgnore]
    public DbUser? SignedOutUser { get; set; }

    public Guid ServiceId { get; set; }

    [MapperIgnore]
    public DbService? Service { get; set; }
}