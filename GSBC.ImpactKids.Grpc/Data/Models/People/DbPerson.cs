using Riok.Mapperly.Abstractions;

namespace GSBC.ImpactKids.Grpc.Data.Models.People;

public class DbPerson
{
    public required Guid Id { get; set; }

    public string? ElvantoId { get; set; }

    public required string FirstName { get; set; }
    public required string LastName  { get; set; }
    
    public required string? PhoneNumber { get; set; }
    public required string? Email       { get; set; }

    public required Guid? SchoolGradeId { get; set; }

    [MapperIgnore]
    public DbSchoolGrade? SchoolGrade { get; set; }

    public required string MediaConsent { get; set; }

    /// <summary>
    /// "Male", "Female", or null when nobody has said. Stored as a string for the same reason
    /// <see cref="MediaConsent"/> is: <c>GenderDescriptor</c> reads and writes it through
    /// <c>IFieldSyncDescriptor</c>'s string interface, and <c>FieldChangeTrackingInterceptor</c>
    /// records this property's EF name in <c>FieldChangeLogs</c> — so the name must stay "Gender".
    ///
    /// Nullable rather than defaulted, because there is no value that means "not told" and inventing
    /// one would give the sync something to push.
    /// </summary>
    public string? Gender { get; set; }

    /// <summary>
    /// The content hash of this person's current photo, or null when they have none.
    ///
    /// <b>A token, never the bytes.</b> The photo itself lives in the object store under this hash;
    /// this column is what puts the version in the serve URL, which is what lets the response be
    /// cached immutably for a year. 1772 people is far too much image data to carry in app state, so
    /// the contract carries a few bytes per person and the browser fetches faces as ordinary images.
    /// </summary>
    public string? PhotoVersion { get; set; }

    /// <summary>Set by anyone who notices an out-of-date face; cleared automatically when a photo is taken.</summary>
    public bool PhotoNeedsUpdate { get; set; }

    public required DateTimeOffset? DateOfBirth { get; set; }
    public required DateTimeOffset? FirstTime   { get; set; }

    [MapperIgnore]
    public List<DbAllergy> Allergies { get; set; } = [];

    [MapperIgnore]
    public List<DbMedicalNote> MedicalNotes { get; set; } = [];

    // family stuff
    public required Guid FamilyId       { get; set; }
    public required bool FamilyGuardian { get; set; }

    [MapperIgnore]
    public DateTimeOffset? DeletedAtUtc { get; set; }
}