using GSBC.ImpactKids.Grpc.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Data;

public partial class GsbcDbContext
{
    public void BuildServiceModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DbSchoolTerm>()
            .HasMany(x => x.Services)
            .WithOne(x => x.SchoolTerm)
            .HasForeignKey(x => x.SchoolTermId);

        modelBuilder.Entity<DbService>()
            .HasOne(x => x.DollarStoreEntry)
            .WithOne(x => x.Service)
            .HasForeignKey<DbDollarStoreEntry>(x => x.ServiceId);
    }
}