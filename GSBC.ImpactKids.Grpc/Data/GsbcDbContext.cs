using GSBC.ImpactKids.Grpc.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Data;

public partial class GsbcDbContext(
    DbContextOptions options
) : DbContext(options)
{
    public required DbSet<DbUser> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<DbUser>()
            .HasIndex(x => x.GoogleSub)
            .IsUnique();

        BuildPeopleModel(modelBuilder);
        BuildScheduleModel(modelBuilder);
        BuildScriptureModel(modelBuilder);
        BuildAttendanceModel(modelBuilder);
        BuildGamesModel(modelBuilder);
        BuildSyncModel(modelBuilder);
    }
}