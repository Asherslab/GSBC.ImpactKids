using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GSBC.ImpactKids.Grpc.Data;

// ReSharper disable once UnusedType.Global
public class GsbcDbContextFactory : IDesignTimeDbContextFactory<GsbcDbContext>
{
    public GsbcDbContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<GsbcDbContext> optionsBuilder = new();
        optionsBuilder.UseNpgsql("Host=localhost;Port=60536;Database=impact-kids;Username=postgres;Password=6R0FCuRT-ca.*uk{Pb7KM3");

        return new GsbcDbContext(optionsBuilder.Options)
        {
            Users = null!,

            People = null!,
            Allergies = null!,
            MedicalNotes = null!,

            SchoolGrades = null!,
            Allergens = null!,
            MedicalTypes = null!,

            AttendanceRecords = null!,
            AttendanceItemTypes = null!,
            AttendanceItemRecords = null!,

            Services = null!,
            ServiceTypes = null!,
            DollarStoreEntries = null!,
            Terms = null!,

            BibleVerses = null!,

            MemoryVerseLists = null!,
            MemoryVerses = null!,
            MemorisationEntries = null!,
            VirtualMemorisationEntries = null!
        };
    }
}