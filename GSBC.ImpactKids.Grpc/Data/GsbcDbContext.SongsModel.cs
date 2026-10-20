using GSBC.ImpactKids.Grpc.Data.Models.Songs;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Data;

public partial class GsbcDbContext
{
    // Songs \\
    public required DbSet<DbSong> Songs { get; set; }

    private static void BuildSongsModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DbSong>()
            .HasIndex(x => x.ProPresenterUuid)
            .IsUnique();

        modelBuilder.Entity<DbSong>()
            .HasOne(x => x.SyncedByMachine)
            .WithMany()
            .HasForeignKey(x => x.SyncedByMachineId)
            // Disconnecting a Mac must not empty the song library. Its songs stay until another Mac
            // sends them, or someone deletes them in ProPresenter and a Mac notices.
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<DbSong>()
            .Property(x => x.ProPresenterUuid)
            .HasMaxLength(64);

        modelBuilder.Entity<DbSong>()
            .Property(x => x.Name)
            .HasMaxLength(200);

        modelBuilder.Entity<DbSong>()
            .Property(x => x.Library)
            .HasMaxLength(200);
    }
}
