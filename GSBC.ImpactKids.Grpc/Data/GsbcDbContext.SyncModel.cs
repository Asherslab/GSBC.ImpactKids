using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Data.Models.Sync;
using GSBC.ImpactKids.Grpc.Data.Models.Sync.Enums;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Data;

public partial class GsbcDbContext
{
    public required DbSet<DbFieldChangeLog>       FieldChangeLogs       { get; set; }
    public required DbSet<DbElvantoFieldSnapshot> ElvantoFieldSnapshots { get; set; }
    public required DbSet<DbSyncMetadata>         SyncMetadata          { get; set; }
    public required DbSet<DbSyncOperation>        SyncOperations        { get; set; }
    public required DbSet<DbSyncAuditLog>         SyncAuditLogs         { get; set; }
    public required DbSet<DbSyncFieldConfig>      SyncFieldConfigs      { get; set; }
    public required DbSet<DbSyncPendingReview>    PendingReviews        { get; set; }

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

        // DbSyncMetadata
        modelBuilder.Entity<DbSyncMetadata>()
            .HasOne(x => x.Person)
            .WithOne()
            .HasForeignKey<DbSyncMetadata>(x => x.PersonId);

        modelBuilder.Entity<DbSyncMetadata>()
            .HasIndex(x => x.ElvantoId)
            .IsUnique();

        modelBuilder.Entity<DbSyncMetadata>()
            .HasIndex(x => x.PersonId)
            .IsUnique();
        modelBuilder.Entity<DbSyncMetadata>()
            .Property(x => x.LastSyncStatus).HasConversion<string>();

        // DbSyncOperation
        modelBuilder.Entity<DbSyncOperation>()
            .HasOne(x => x.Person)
            .WithMany()
            .HasForeignKey(x => x.PersonId)
            .IsRequired(false);
        modelBuilder.Entity<DbSyncOperation>()
            .HasIndex(x => new { x.Scope, x.StartedAt });
        modelBuilder.Entity<DbSyncOperation>()
            .Property(x => x.Mode).HasConversion<string>();
        modelBuilder.Entity<DbSyncOperation>()
            .Property(x => x.Scope).HasConversion<string>();
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

        // DbSyncFieldConfig — unique per entity+field
        modelBuilder.Entity<DbSyncFieldConfig>()
            .HasIndex(x => new { x.EntityType, x.FieldName })
            .IsUnique();
        modelBuilder.Entity<DbSyncFieldConfig>()
            .Property(x => x.Direction).HasConversion<string>();
        modelBuilder.Entity<DbSyncFieldConfig>()
            .Property(x => x.PrecedenceOnTie).HasConversion<string>();

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

        SeedSyncFieldConfigs(modelBuilder);
    }

    private static void SeedSyncFieldConfigs(ModelBuilder modelBuilder)
    {
        static DbSyncFieldConfig Cfg(string field, SyncDirection dir, PrecedenceOnTie prec) => new()
        {
            Id = GuidForField(field),
            EntityType = "Person",
            FieldName = field,
            Direction = dir,
            PrecedenceOnTie = prec
        };

        modelBuilder.Entity<DbSyncFieldConfig>().HasData(
            Cfg("FirstName", SyncDirection.Bidirectional, PrecedenceOnTie.Elvanto),
            Cfg("LastName", SyncDirection.Bidirectional, PrecedenceOnTie.Elvanto),
            Cfg("Email", SyncDirection.Bidirectional, PrecedenceOnTie.Elvanto),
            Cfg("PhoneNumber", SyncDirection.Bidirectional, PrecedenceOnTie.Elvanto),
            Cfg("DateOfBirth", SyncDirection.Bidirectional, PrecedenceOnTie.Elvanto),
            Cfg("FirstTime", SyncDirection.Bidirectional, PrecedenceOnTie.Elvanto),
            Cfg("MediaConsent", SyncDirection.Bidirectional, PrecedenceOnTie.Elvanto),
            // Elvanto owns school grade IDs and SchoolGradeDescriptor pushes nothing, so
            // Bidirectional here made a grade change take the outbound branch and write a
            // "would push" row naming the local Guid, for a request body that never carried it.
            Cfg("SchoolGradeId", SyncDirection.InboundOnly, PrecedenceOnTie.Elvanto),
            // Family membership moves both ways now that Elvanto's date_modified makes "which side
            // changed later" answerable. This row is what actually decides it: FamilyIdDescriptor
            // being Bidirectional counted for nothing while this said InboundOnly, and the move was
            // dropped with no audit row to say so.
            Cfg("FamilyId", SyncDirection.Bidirectional, PrecedenceOnTie.Elvanto),
            Cfg("FamilyGuardian", SyncDirection.InboundOnly, PrecedenceOnTie.Elvanto),
            // One row for the one Elvanto field. The old Allergies/MedicalNotes rows named
            // descriptors that no longer exist - both wrote to this same custom field - and a
            // stale row is not harmless: a config row overrides the descriptor's
            // DefaultDirection entirely, so it silently decides behaviour for a field nothing
            // reads any more.
            Cfg("MedicalAllergyNotes", SyncDirection.Bidirectional, PrecedenceOnTie.App)
        );
    }

    // Deterministic GUIDs so migrations are stable across rebuilds
    private static Guid GuidForField(string field)
    {
        byte[] hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes($"SyncFieldConfig:Person:{field}"));
        return new Guid(hash[..16]);
    }
}