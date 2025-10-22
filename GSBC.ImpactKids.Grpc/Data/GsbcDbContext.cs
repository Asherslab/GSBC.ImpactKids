using GSBC.ImpactKids.Grpc.Data.Models;
using GSBC.ImpactKids.Grpc.Data.Models.MemoryVerses;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Data;

public class GsbcDbContext(
    DbContextOptions options
) : DbContext(options)
{
    public required DbSet<DbUser> Users { get; set; }
    
    public required DbSet<DbSchoolTerm> Terms    { get; set; }
    public required DbSet<DbService>    Services { get; set; }

    public required DbSet<DbBibleVerse> BibleVerses { get; set; }

    public required DbSet<DbMemoryVerseList> MemoryVerseLists { get; set; }
    public required DbSet<DbMemoryVerse>     MemoryVerses     { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<DbUser>()
            .HasIndex(x => x.GoogleSub)
            .IsUnique();
        
        modelBuilder.Entity<DbSchoolTerm>()
            .HasMany(x => x.Services)
            .WithOne(x => x.SchoolTerm)
            .HasForeignKey(x => x.SchoolTermId);

        modelBuilder.Entity<DbMemoryVerseBibleVerseRelationship>()
            .HasKey(x => new { x.MemoryVersesId, x.BibleVersesId });

        modelBuilder.Entity<DbMemoryVerseServiceRelationship>()
            .HasKey(x => new { x.MemoryVersesId, x.ServicesId });

        modelBuilder.Entity<DbBibleVerse>()
            .HasMany(x => x.MemoryVerses)
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