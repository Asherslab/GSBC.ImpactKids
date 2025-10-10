using GSBC.ImpactKids.Grpc.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Data;

public class GsbcDbContext(
    DbContextOptions options
) : DbContext(options)
{
    public required DbSet<DbSchoolTerm> Terms { get; set; }
    public required DbSet<DbService> Services { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<DbSchoolTerm>()
            .HasMany(x => x.Services)
            .WithOne(x => x.SchoolTerm)
            .HasForeignKey(x => x.SchoolTermId);
    }
}