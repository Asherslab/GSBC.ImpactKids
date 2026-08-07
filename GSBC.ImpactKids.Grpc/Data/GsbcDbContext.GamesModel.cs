using GSBC.ImpactKids.Grpc.Data.Models.Games;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Data;

public partial class GsbcDbContext
{
    // Games \\
    public required DbSet<DbGamePointRecord> GamePointRecords { get; set; }
    public required DbSet<DbGameBoard>       GameBoards       { get; set; }

    private static void BuildGamesModel(ModelBuilder modelBuilder)
    {
        // Ids come from the client so an offline device can queue records and
        // resend them safely - never let the database assign one.
        modelBuilder.Entity<DbGamePointRecord>()
            .Property(x => x.Id)
            .ValueGeneratedNever();

        modelBuilder.Entity<DbGamePointRecord>()
            .HasOne(x => x.Service)
            .WithMany()
            .HasForeignKey(x => x.ServiceId);

        modelBuilder.Entity<DbGamePointRecord>()
            .HasOne(x => x.AwardedUser)
            .WithMany()
            .HasForeignKey(x => x.AwardedUserId);

        modelBuilder.Entity<DbGamePointRecord>()
            .HasIndex(x => x.ServiceId);

        modelBuilder.Entity<DbGameBoard>()
            .HasOne(x => x.Service)
            .WithMany()
            .HasForeignKey(x => x.ServiceId);

        // One board per service - the upsert relies on this.
        modelBuilder.Entity<DbGameBoard>()
            .HasIndex(x => x.ServiceId)
            .IsUnique();
    }
}
