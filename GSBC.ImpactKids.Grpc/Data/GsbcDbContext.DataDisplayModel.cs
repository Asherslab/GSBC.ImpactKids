using GSBC.ImpactKids.Grpc.Data.Models.DataDisplay;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Data;

public partial class GsbcDbContext
{
    // Data Display \\
    public required DbSet<DbMetabaseEmbed> MetabaseEmbeds { get; set; }

    private static void BuildDataDisplayModel(ModelBuilder modelBuilder)
    {
        // The label is the page's address, /Analytics/{Label}, so two with one label would hide one.
        modelBuilder.Entity<DbMetabaseEmbed>()
            .HasIndex(x => x.Label)
            .IsUnique();

        modelBuilder.Entity<DbMetabaseEmbed>()
            .Property(x => x.Label)
            .HasMaxLength(200);

        modelBuilder.Entity<DbMetabaseEmbed>()
            .Property(x => x.Type)
            .HasConversion<string>()
            .HasMaxLength(32);
    }
}
