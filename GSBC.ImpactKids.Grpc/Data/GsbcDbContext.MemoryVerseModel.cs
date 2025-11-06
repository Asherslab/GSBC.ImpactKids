using GSBC.ImpactKids.Grpc.Data.Models;
using GSBC.ImpactKids.Grpc.Data.Models.MemoryVerses;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Data;

public partial class GsbcDbContext
{
    public void BuildMemoryVerseModel(ModelBuilder modelBuilder)
    {
        // relationship objects
        modelBuilder.Entity<DbMemoryVerseBibleVerseRelationship>()
            .HasKey(x => new { x.MemoryVersesId, x.BibleVersesId });

        modelBuilder.Entity<DbMemoryVerseServiceRelationship>()
            .HasKey(x => new { x.MemoryVersesId, x.ServicesId });

        // memory verse stuff
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

        // memorisation entries
        modelBuilder.Entity<DbMemorisationEntry>()
            .HasIndex(x => new { x.PersonId, x.ServiceId, x.MemoryVerseId })
            .IsUnique();

        modelBuilder.Entity<DbMemorisationEntry>()
            .HasOne(x => x.Person)
            .WithMany()
            .HasForeignKey(x => x.PersonId);

        modelBuilder.Entity<DbMemorisationEntry>()
            .HasOne(x => x.Service)
            .WithMany()
            .HasForeignKey(x => x.ServiceId);

        modelBuilder.Entity<DbMemorisationEntry>()
            .HasOne(x => x.MemoryVerse)
            .WithMany(x => x.MemorisationEntries)
            .HasForeignKey(x => x.MemoryVerseId);
    }
}