using System.Reflection;
using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Features.People.Sync.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace GSBC.ImpactKids.Grpc.Tests.Sync;

/// <summary>
/// Three independent authorities have to agree on every field name, and nothing asserted it.
///
/// A descriptor's <c>FieldName</c> is the name <c>FieldChangeTrackingInterceptor</c> writes into
/// <c>FieldChangeLogs</c> — which it takes from EF's property name on <c>DbPerson</c>. Rename
/// <c>DbPerson.FirstTime</c> and no change-log row is ever found for that field again, so it can
/// never win a conflict. There is no error anywhere. This is the test that turns that into a red
/// build.
///
/// The third authority, a seeded <c>SyncFieldConfigs</c> row, is gone: direction and tie-breaking
/// now live on the descriptor, which is where the code already had them.
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

}
