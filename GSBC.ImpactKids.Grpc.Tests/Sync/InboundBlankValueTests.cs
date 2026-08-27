using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Features.People.Sync;
using GSBC.ImpactKids.Grpc.Features.People.Sync.Descriptors;
using GSBC.ImpactKids.Grpc.Features.People.Sync.Interfaces;

namespace GSBC.ImpactKids.Grpc.Tests.Sync;

/// <summary>
/// "Elvanto holds nothing" is not a value, and a descriptor that will not take a value must say so.
///
/// Both halves of that were untrue and cost a real run. <c>IsValidInboundValue</c> defaulted to
/// true, so a blank Elvanto value won <c>FirstSync:ElvantoPrecedence</c> and planned an inbound
/// <i>clear</i> - and <c>SetOnApp</c> returned void, so a descriptor that then refused the blank did
/// so silently, the change was marked Applied, and the base was settled as though the person held
/// it. The next run read the resulting gap as an app-side edit and planned to push it to Elvanto.
///
/// Written over every descriptor by reflection, like <see cref="FieldNameParityTests"/>, so a new
/// one is covered without touching this file.
/// </summary>
public class InboundBlankValueTests
{
    public static TheoryData<string> Names()
    {
        TheoryData<string> data = [];
        foreach (IFieldSyncDescriptor d in All) data.Add(d.FieldName);
        return data;
    }

    private static readonly IReadOnlyList<IFieldSyncDescriptor> All =
        typeof(IFieldSyncDescriptor).Assembly
            .GetTypes()
            .Where(t => t.IsAssignableTo(typeof(IFieldSyncDescriptor)) && t is { IsClass: true, IsAbstract: false })
            .Select(Build)
            .OrderBy(d => d.FieldName, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// The medical/allergy descriptor throws rather than run without its lookups, which is right -
    /// without them an inbound write drops every allergen. It gets an empty set here so the refusals
    /// below are the thing being measured.
    /// </summary>
    private static IFieldSyncDescriptor Build(Type t)
    {
        IFieldSyncDescriptor d = (IFieldSyncDescriptor)Activator.CreateInstance(t)!;
        if (d is MedicalAllergyNotesDescriptor m)
            m.Lookups = new MedicalAllergyLookups
            {
                AllergenLabels     = new Dictionary<Guid, string>(),
                MedicalTypeLabels  = new Dictionary<Guid, string>(),
                OtherMedicalTypeId = Guid.NewGuid()
            };
        return d;
    }

    private static IFieldSyncDescriptor Get(string name) => All.Single(d => d.FieldName == name);

    [Theory]
    [MemberData(nameof(Names))]
    public void ABlankElvantoValueIsNeverUsableInbound(string fieldName)
    {
        IFieldSyncDescriptor d = Get(fieldName);

        Assert.False(d.IsValidInboundValue(null));
        Assert.False(d.IsValidInboundValue(""));
        Assert.False(d.IsValidInboundValue("   "));
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void ARefusedInboundValueChangesNothingAndSaysSo(string fieldName)
    {
        IFieldSyncDescriptor d = Get(fieldName);
        DbPerson person = APersonWithRealData();

        string? before = d.GetFromApp(person);

        // The reconciler will not hand a blank to SetOnApp any more, but a descriptor is the
        // backstop for that and has to hold on its own.
        foreach (string? blank in new[] { null, "", "   " })
        {
            Assert.False(d.SetOnApp(person, blank));
            Assert.Equal(before, d.GetFromApp(person));
        }
    }

    [Fact]
    public void AValueTheDescriptorCannotReadIsRefusedRatherThanGuessedAt()
    {
        // Each of these used to be a silent no-op or, worse, a clear: an unparseable date left the
        // birthday alone and reported success, and a school grade with no local row cleared the
        // child's year level outright.
        DbPerson person = APersonWithRealData();

        Assert.False(Get("DateOfBirth").SetOnApp(person, "not-a-date"));
        Assert.Equal("2017-03-06", Get("DateOfBirth").GetFromApp(person));

        Assert.False(Get("FirstTime").SetOnApp(person, "05-08"));

        Assert.False(Get("SchoolGradeId").SetOnApp(person, "Year 3"));
        Assert.NotNull(person.SchoolGradeId);

        Assert.False(Get("FamilyId").SetOnApp(person, "42"));
        Assert.NotEqual(Guid.Empty, person.FamilyId);
    }

    [Fact]
    public void AValueTheDescriptorCanReadIsTakenAndSaidSo()
    {
        DbPerson person = APersonWithRealData();

        Assert.True(Get("DateOfBirth").SetOnApp(person, "2015-01-02"));
        Assert.Equal("2015-01-02", Get("DateOfBirth").GetFromApp(person));

        Assert.True(Get("Email").SetOnApp(person, "new@example.com"));
        Assert.Equal("new@example.com", person.Email);

        // A blank family_id reaches this descriptor as Guid.Empty spelled out, not as a blank, so
        // "no household" is still a value the app takes.
        Assert.True(Get("FamilyId").SetOnApp(person, Guid.Empty.ToString()));
        Assert.Equal(Guid.Empty, person.FamilyId);
    }

    private static DbPerson APersonWithRealData() => new()
    {
        Id             = Guid.NewGuid(),
        FirstName      = "Zzztestperson",
        LastName       = "Writetest",
        Email          = "real@example.com",
        PhoneNumber    = "0435862120",
        DateOfBirth    = new DateTimeOffset(new DateTime(2017, 3, 6, 0, 0, 0, DateTimeKind.Utc)),
        FirstTime      = new DateTimeOffset(new DateTime(2024, 5, 1, 0, 0, 0, DateTimeKind.Utc)),
        SchoolGradeId  = Guid.NewGuid(),
        MediaConsent   = "Granted",
        FamilyId       = Guid.NewGuid(),
        FamilyGuardian = true
    };
}
