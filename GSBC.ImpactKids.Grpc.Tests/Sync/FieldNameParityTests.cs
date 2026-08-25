using System.Reflection;
using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Data.Models.Sync;
using GSBC.ImpactKids.Grpc.Features.People.Sync.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace GSBC.ImpactKids.Grpc.Tests.Sync;

/// <summary>
/// Three independent authorities have to agree on every field name, and nothing asserted it.
///
/// A descriptor's <c>FieldName</c> is the key the seeded <c>SyncFieldConfigs</c> row is looked up
/// by, and it is also the name <c>FieldChangeTrackingInterceptor</c> writes into
/// <c>FieldChangeLogs</c> — which it takes from EF's property name on <c>DbPerson</c>. Rename
/// <c>DbPerson.FirstTime</c> and that field's sync breaks silently and permanently: no config row
/// matches, so it falls back to a default direction, and no change-log row is ever found for it.
/// There is no error anywhere. This is the test that turns that into a red build.
/// </summary>
public class FieldNameParityTests
{
    /// <summary>
    /// The design-time model, not the runtime one. Seed data is compiled out of the read-optimised
    /// model, and asking the runtime model for it throws rather than returning nothing — which would
    /// otherwise read as "there are no seeded rows" and pass.
    /// </summary>
    private static readonly IModel Model =
        new GsbcDbContextFactory().CreateDbContext([]).GetService<IDesignTimeModel>().Model;

    /// <summary>
    /// Every descriptor in the assembly, built by reflection the same way
    /// <c>AddPeopleSync</c> registers them, so a new descriptor is covered without touching this file.
    /// </summary>
    public static TheoryData<string> DescriptorFieldNames()
    {
        TheoryData<string> data = [];
        foreach (IFieldSyncDescriptor descriptor in Descriptors)
            data.Add(descriptor.FieldName);
        return data;
    }

    private static readonly IReadOnlyList<IFieldSyncDescriptor> Descriptors =
        typeof(IFieldSyncDescriptor).Assembly
            .GetTypes()
            .Where(t => t.IsAssignableTo(typeof(IFieldSyncDescriptor)) && t is { IsClass: true, IsAbstract: false })
            .Select(t => (IFieldSyncDescriptor)Activator.CreateInstance(t)!)
            .OrderBy(d => d.FieldName, StringComparer.Ordinal)
            .ToList();

    [Fact]
    public void ThereAreDescriptorsToCheck()
    {
        // Guards the rest of this file: reflection finding nothing would make every theory below
        // pass vacuously, which is worse than no test.
        Assert.NotEmpty(Descriptors);
    }

    [Theory]
    [MemberData(nameof(DescriptorFieldNames))]
    public void EveryDescriptorFieldNameIsAPropertyOnDbPerson(string fieldName)
    {
        // The medical/allergy field is the one deliberate exception: the app holds it as two child
        // tables, and the interceptor logs those against the person under this name on purpose.
        if (fieldName == "MedicalAllergyNotes")
        {
            Assert.NotNull(typeof(DbPerson).GetProperty(nameof(DbPerson.Allergies), BindingFlags.Public | BindingFlags.Instance));
            Assert.NotNull(typeof(DbPerson).GetProperty(nameof(DbPerson.MedicalNotes), BindingFlags.Public | BindingFlags.Instance));
            return;
        }

        IEntityType person = Model.FindEntityType(typeof(DbPerson))!;

        Assert.True(
            person.FindProperty(fieldName) is not null,
            $"Descriptor field '{fieldName}' has no matching EF property on DbPerson. The change "
            + "interceptor writes EF's property name into FieldChangeLogs, so this field's app-side "
            + "edits would never be found and its sync would break silently.");
    }

    [Theory]
    [MemberData(nameof(DescriptorFieldNames))]
    public void EveryDescriptorHasASeededFieldConfigRow(string fieldName)
    {
        Assert.Contains(fieldName, SeededFieldNames());
    }

    [Fact]
    public void EverySeededFieldConfigRowHasADescriptor()
    {
        // The other direction, and not symmetric noise: a config row overrides a descriptor's
        // DefaultDirection outright, so a row naming a descriptor that no longer exists silently
        // decides behaviour for a field nothing reads. Two migrations were needed to clear the last
        // pair of those, one of which cost a family move dropped with no audit row.
        HashSet<string> descriptorNames = Descriptors.Select(d => d.FieldName).ToHashSet(StringComparer.Ordinal);

        foreach (string seeded in SeededFieldNames())
            Assert.Contains(seeded, descriptorNames);
    }

    [Fact]
    public void SeededDirectionsMatchTheDescriptorDefaults()
    {
        // Not a rule, a tripwire. A seed row that disagrees with its descriptor is legitimate - the
        // row is what actually decides - but every one of the eleven agrees today, so a new
        // disagreement is far more likely to be a mistake than a decision.
        IEntityType config = Model.FindEntityType(typeof(DbSyncFieldConfig))!;

        foreach (IDictionary<string, object?> row in config.GetSeedData())
        {
            string name = (string)row["FieldName"]!;
            IFieldSyncDescriptor descriptor = Descriptors.Single(d => d.FieldName == name);

            Assert.Equal(descriptor.DefaultDirection.ToString(), row["Direction"]!.ToString());
        }
    }

    private static IEnumerable<string> SeededFieldNames() =>
        Model.FindEntityType(typeof(DbSyncFieldConfig))!
            .GetSeedData()
            .Select(row => (string)row["FieldName"]!);
}
