using GSBC.ImpactKids.Grpc.Data.Models.ProPresenter;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Data;

public partial class GsbcDbContext
{
    // ProPresenter \\
    public required DbSet<DbProPresenterMachine> ProPresenterMachines { get; set; }

    private static void BuildProPresenterModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DbProPresenterMachine>()
            .HasOne(x => x.CreatedByUser)
            .WithMany()
            .HasForeignKey(x => x.CreatedByUserId)
            // A leader leaving must not take the projector down with them.
            .OnDelete(DeleteBehavior.SetNull);

        // The token endpoint finds a pending enrolment by its code alone.
        modelBuilder.Entity<DbProPresenterMachine>()
            .HasIndex(x => x.EnrolmentCodeHash)
            .IsUnique();

        modelBuilder.Entity<DbProPresenterMachine>()
            .Property(x => x.Name)
            .HasMaxLength(100);

        modelBuilder.Entity<DbProPresenterMachine>()
            .Property(x => x.ProPresenterHost)
            .HasMaxLength(255);
    }
}
