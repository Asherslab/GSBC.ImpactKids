using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GSBC.ImpactKids.Grpc.Data;

public class GsbcDbContextFactory : IDesignTimeDbContextFactory<GsbcDbContext>
{
    public GsbcDbContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<GsbcDbContext> optionsBuilder = new();
        optionsBuilder.UseNpgsql("Host=localhost;Database=impact-kids;Username=postgres;Password=Password123");

        return new GsbcDbContext(optionsBuilder.Options)
        {
            Terms = null!,
            Services = null!
        };
    }
}