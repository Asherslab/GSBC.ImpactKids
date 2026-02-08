using GSBC.ImpactKids.Grpc.Data.Models;
using GSBC.ImpactKids.Grpc.Data.Models.Scheduling;
using GSBC.ImpactKids.Grpc.Data.Models.Scheduling.School;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Data;

public partial class GsbcDbContext
{
    // Schedule \\
    public required DbSet<DbService>          Services           { get; set; }
    public required DbSet<DbServiceType>      ServiceTypes       { get; set; }
    public required DbSet<DbDollarStoreEntry> DollarStoreEntries { get; set; }
    public required DbSet<DbSchoolTerm>       Terms              { get; set; }

    private static void BuildScheduleModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DbSchoolTerm>()
            .HasMany(x => x.Services)
            .WithOne(x => x.SchoolTerm)
            .HasForeignKey(x => x.SchoolTermId);

        modelBuilder.Entity<DbService>()
            .HasOne(x => x.DollarStoreEntry)
            .WithOne(x => x.Service)
            .HasForeignKey<DbDollarStoreEntry>(x => x.ServiceId);

        modelBuilder.Entity<DbService>()
            .HasOne(x => x.ServiceType)
            .WithMany()
            .HasForeignKey(x => x.ServiceTypeId);
    }
}