using Riok.Mapperly.Abstractions;

namespace GSBC.ImpactKids.Grpc.Data.Models.People;

public class DbPerson
{
    public required Guid Id { get; set; }

    [MapperIgnore]
    public string? ElvantoId { get; set; }

    public required string FirstName { get; set; }
    public required string LastName  { get; set; }
    
    public required string? PhoneNumber { get; set; }
    public required string? Email       { get; set; }

    public required Guid? SchoolGradeId { get; set; }

    [MapperIgnore]
    public DbSchoolGrade? SchoolGrade { get; set; }

    public required string MediaConsent { get; set; }

    public required DateTimeOffset? DateOfBirth { get; set; }
    public required DateTimeOffset? FirstTime   { get; set; }

    [MapperIgnore]
    public List<DbAllergy> Allergies { get; set; } = [];

    [MapperIgnore]
    public List<DbMedicalNote> MedicalNotes { get; set; } = [];

    // family stuff
    public required Guid FamilyId       { get; set; }
    public required bool FamilyGuardian { get; set; }
}