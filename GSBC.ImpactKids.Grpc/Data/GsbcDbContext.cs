using GSBC.ImpactKids.Grpc.Data.Models;
using GSBC.ImpactKids.Grpc.Data.Models.MemoryVerses;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Data;

public class GsbcDbContext(
    DbContextOptions options
) : DbContext(options)
{
    public required DbSet<DbSchoolTerm> Terms    { get; set; }
    public required DbSet<DbService>    Services { get; set; }

    public required DbSet<DbBibleVerse> BibleVerses { get; set; }

    public required DbSet<DbMemoryVerseList> MemoryVerseLists { get; set; }
    public required DbSet<DbMemoryVerse>     MemoryVerses     { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<DbSchoolTerm>()
            .HasMany(x => x.Services)
            .WithOne(x => x.SchoolTerm)
            .HasForeignKey(x => x.SchoolTermId);

        modelBuilder.Entity<DbMemoryVerseBibleVerseRelationship>()
            .HasKey(x => new { x.MemoryVerseId, x.BibleVerseId });

        modelBuilder.Entity<DbMemoryVerseServiceRelationship>()
            .HasKey(x => new { x.MemoryVerseId, x.ServiceId });

        modelBuilder.Entity<DbBibleVerse>()
            .HasMany<DbMemoryVerse>()
            .WithMany(x => x.BibleVerses)
            .UsingEntity<DbMemoryVerseBibleVerseRelationship>();

        modelBuilder.Entity<DbMemoryVerseList>()
            .HasMany(x => x.MemoryVerses)
            .WithOne(x => x.MemoryVerseList)
            .HasForeignKey(x => x.MemoryVerseListId);
        
        modelBuilder.Entity<DbMemoryVerse>()
            .HasMany(x => x.Services)
            .WithMany(x => x.MemoryVerses)
            .UsingEntity<DbMemoryVerseServiceRelationship>();
    }
}