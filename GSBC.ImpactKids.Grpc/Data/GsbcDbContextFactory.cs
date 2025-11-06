using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GSBC.ImpactKids.Grpc.Data;

public class GsbcDbContextFactory : IDesignTimeDbContextFactory<GsbcDbContext>
{
    public GsbcDbContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<GsbcDbContext> optionsBuilder = new();
        optionsBuilder.UseNpgsql("Host=localhost;Database=impact-kids;Username=postgres;Password=Password123");

        return new GsbcDbContext(optionsBuilder.Options)
        {
            Users = null!,
            
            People = null!,
            Allergies = null!,
            MedicalNotes = null!,
            
            SchoolGrades = null!,
            Allergens = null!,
            MedicalTypes = null!,
            
            Terms = null!,
            Services = null!,
            DollarStoreEntries = null!,

            BibleVerses = null!,
            
            MemoryVerseLists = null!,
            MemoryVerses = null!,
            MemorisationEntries = null!,
        };
    }
}