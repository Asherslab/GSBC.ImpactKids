using GSBC.ImpactKids.Grpc.Data.Models;
using GSBC.ImpactKids.Grpc.Data.Models.MemoryVerses;
using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Data.Models.Scheduling;
using GSBC.ImpactKids.Grpc.Data.Models.Scheduling.School;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Data;

public partial class GsbcDbContext(
    DbContextOptions options
) : DbContext(options)
{
    public required DbSet<DbUser> Users { get; set; }

    // People \\
    public required DbSet<DbPerson>      People       { get; set; }
    public required DbSet<DbAllergy>     Allergies    { get; set; }
    public required DbSet<DbMedicalNote> MedicalNotes { get; set; }

    // People Data \\
    public required DbSet<DbSchoolGrade> SchoolGrades { get; set; }
    public required DbSet<DbAllergen>    Allergens    { get; set; }
    public required DbSet<DbMedicalType> MedicalTypes { get; set; }

    // Schedule \\
    public required DbSet<DbService>          Services           { get; set; }
    public required DbSet<DbServiceType>      ServiceTypes       { get; set; }
    public required DbSet<DbDollarStoreEntry> DollarStoreEntries { get; set; }
    public required DbSet<DbSchoolTerm>       Terms              { get; set; }

    // Memory Verses \\
    public required DbSet<DbBibleVerse>               BibleVerses                { get; set; }
    public required DbSet<DbMemoryVerseList>          MemoryVerseLists           { get; set; }
    public required DbSet<DbMemoryVerse>              MemoryVerses               { get; set; }
    public required DbSet<DbMemorisationEntry>        MemorisationEntries        { get; set; }
    public required DbSet<DbVirtualMemorisationEntry> VirtualMemorisationEntries { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<DbUser>()
            .HasIndex(x => x.GoogleSub)
            .IsUnique();

        BuildPeopleModel(modelBuilder);
        BuildScheduleModel(modelBuilder);
        BuildScriptureModel(modelBuilder);
    }
}