using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Data.Models.Sync;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Data;

public partial class GsbcDbContext
{
    public required DbSet<DbFieldChangeLog>       FieldChangeLogs       { get; set; }
    public required DbSet<DbElvantoFieldSnapshot> ElvantoFieldSnapshots { get; set; }
    public required DbSet<DbSyncOperation>        SyncOperations        { get; set; }
    public required DbSet<DbSyncAuditLog>         SyncAuditLogs         { get; set; }
    public required DbSet<DbSyncPendingReview>    PendingReviews        { get; set; }
    public required DbSet<DbSyncPlannedChange>    PlannedChanges        { get; set; }
    public required DbSet<DbElvantoFamilyLink>    ElvantoFamilyLinks    { get; set; }

    private static void BuildSyncModel(ModelBuilder modelBuilder)
    {
        // soft-delete filter on DbPerson
        modelBuilder.Entity<DbPerson>()
            .HasQueryFilter(x => x.DeletedAtUtc == null);

        // DbFieldChangeLog
        modelBuilder.Entity<DbFieldChangeLog>()
            .HasIndex(x => new { x.EntityType, x.EntityId, x.FieldName, x.ChangedAt });
        modelBuilder.Entity<DbFieldChangeLog>()
            .Property(x => x.Source).HasConversion<string>();

        // DbElvantoFieldSnapshot — unique per entity+field
        modelBuilder.Entity<DbElvantoFieldSnapshot>()
            .HasIndex(x => new { x.EntityType, x.EntityId, x.FieldName })
            .IsUnique();

        // DbSyncOperation - every run covers the whole roll, so StartedAt alone is the useful order.
        modelBuilder.Entity<DbSyncOperation>()
            .HasIndex(x => x.StartedAt);
        modelBuilder.Entity<DbSyncOperation>()
            .Property(x => x.Status).HasConversion<string>();

        // DbSyncAuditLog
        modelBuilder.Entity<DbSyncAuditLog>()
            .HasOne(x => x.SyncOperation)
            .WithMany(x => x.AuditLogs)
            .HasForeignKey(x => x.SyncOperationId);

        modelBuilder.Entity<DbSyncAuditLog>()
            .HasIndex(x => new { x.SyncOperationId, x.OccurredAt });

        modelBuilder.Entity<DbSyncAuditLog>()
            .HasIndex(x => new { x.PersonId, x.OccurredAt });
        modelBuilder.Entity<DbSyncAuditLog>()
            .Property(x => x.EventType).HasConversion<string>();
        modelBuilder.Entity<DbSyncAuditLog>()
            .Property(x => x.Direction).HasConversion<string>();

        // DbSyncPendingReview — unique per (PersonId, ElvantoId) pair, and deliberately
        // one-to-MANY. WithOne() put a unique index on PersonId alone, so a person could hold
        // only a single review for all time. Decided reviews are never deleted (an approved
        // duplicate is the record that two people are the same, and the merge feature will read
        // exactly those rows), so that slot is permanently occupied the moment anyone is judged
        // a duplicate. A later low-confidence match for the same person against a different
        // Elvanto record would then violate the index and fail the entire sync run.
        modelBuilder.Entity<DbSyncPendingReview>()
            .HasOne(x => x.Person)
            .WithMany()
            .HasForeignKey(x => x.PersonId);
        modelBuilder.Entity<DbSyncPendingReview>()
            .HasIndex(x => new { x.PersonId, x.ElvantoId })
            .IsUnique();
        modelBuilder.Entity<DbSyncPendingReview>()
            .Property(x => x.Status).HasConversion<string>();
        modelBuilder.Entity<DbSyncPendingReview>()
            .HasOne(x => x.SyncOperation)
            .WithMany()
            .HasForeignKey(x => x.SyncOperationId)
            .IsRequired(false);

        // DbSyncPlannedChange - the plan a run decided, read back by Apply.
        modelBuilder.Entity<DbSyncPlannedChange>()
            .HasOne(x => x.SyncOperation)
            .WithMany(x => x.PlannedChanges)
            .HasForeignKey(x => x.SyncOperationId);

        // Nullable, because a CreateLocally item names an Elvanto record and no app person: Decide
        // writes nothing to People, so there is no id to point at until Apply makes one.
        modelBuilder.Entity<DbSyncPlannedChange>()
            .HasOne(x => x.Person)
            .WithMany()
            .HasForeignKey(x => x.PersonId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DbSyncPlannedChange>()
            .HasIndex(x => new { x.SyncOperationId, x.Status });
        modelBuilder.Entity<DbSyncPlannedChange>()
            .HasIndex(x => new { x.PersonId, x.DecidedAt });
        modelBuilder.Entity<DbSyncPlannedChange>()
            .Property(x => x.Kind).HasConversion<string>();
        modelBuilder.Entity<DbSyncPlannedChange>()
            .Property(x => x.Status).HasConversion<string>();

        // DbElvantoFamilyLink - unique on BOTH sides, deliberately. One local family is one Elvanto
        // household; a second row for either side is not a better guess, it is two answers to a
        // question that has one, and the constraint is what turns that into a failure someone can
        // see rather than whichever row the dictionary happened to yield first.
        modelBuilder.Entity<DbElvantoFamilyLink>()
            .HasIndex(x => x.LocalFamilyId).IsUnique();
        modelBuilder.Entity<DbElvantoFamilyLink>()
            .HasIndex(x => x.ElvantoFamilyId).IsUnique();
        modelBuilder.Entity<DbElvantoFamilyLink>()
            .Property(x => x.Source).HasConversion<string>();
    }
}