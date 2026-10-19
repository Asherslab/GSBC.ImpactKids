using GSBC.ImpactKids.Grpc.Data.Models.Scheduling.Schedules;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Data;

public partial class GsbcDbContext
{
    // Schedules \\
    public required DbSet<DbScheduleItemTemplate>   ScheduleItemTemplates   { get; set; }
    public required DbSet<DbSchedule>               Schedules               { get; set; }
    public required DbSet<DbScheduleGroup>          ScheduleGroups          { get; set; }
    public required DbSet<DbScheduleBlock>          ScheduleBlocks          { get; set; }
    public required DbSet<DbScheduleLane>           ScheduleLanes           { get; set; }
    public required DbSet<DbScheduleItem>           ScheduleItems           { get; set; }
    public required DbSet<DbScheduleItemAdjustment> ScheduleItemAdjustments { get; set; }
    public required DbSet<DbScheduleRunStep>        ScheduleRunSteps        { get; set; }

    private static void BuildSchedulesModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DbScheduleItemTemplate>()
            .Property(x => x.Name)
            .HasMaxLength(100);

        // Deleting a service takes its schedule with it; the schedule means nothing without the night.
        modelBuilder.Entity<DbSchedule>()
            .HasOne(x => x.Service)
            .WithMany()
            .HasForeignKey(x => x.ServiceId)
            .OnDelete(DeleteBehavior.Cascade);

        // One schedule per service.
        modelBuilder.Entity<DbSchedule>()
            .HasIndex(x => x.ServiceId)
            .IsUnique();

        modelBuilder.Entity<DbSchedule>()
            .HasOne(x => x.DefaultForServiceType)
            .WithMany()
            .HasForeignKey(x => x.DefaultForServiceTypeId)
            .OnDelete(DeleteBehavior.SetNull);

        // At most one default template per service type.
        modelBuilder.Entity<DbSchedule>()
            .HasIndex(x => x.DefaultForServiceTypeId)
            .IsUnique();

        modelBuilder.Entity<DbSchedule>()
            .Property(x => x.Name)
            .HasMaxLength(100);

        modelBuilder.Entity<DbScheduleGroup>()
            .HasOne(x => x.Schedule)
            .WithMany(x => x.Groups)
            .HasForeignKey(x => x.ScheduleId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DbScheduleGroup>()
            .Property(x => x.Name)
            .HasMaxLength(50);

        modelBuilder.Entity<DbScheduleBlock>()
            .HasOne(x => x.Schedule)
            .WithMany(x => x.Blocks)
            .HasForeignKey(x => x.ScheduleId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DbScheduleLane>()
            .HasOne(x => x.Block)
            .WithMany(x => x.Lanes)
            .HasForeignKey(x => x.BlockId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DbScheduleItem>()
            .HasOne(x => x.Lane)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.LaneId)
            .OnDelete(DeleteBehavior.Cascade);

        // Items under a section go with it.
        modelBuilder.Entity<DbScheduleItem>()
            .HasOne(x => x.ParentItem)
            .WithMany(x => x.Children)
            .HasForeignKey(x => x.ParentItemId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DbScheduleItem>()
            .HasOne(x => x.ItemTemplate)
            .WithMany()
            .HasForeignKey(x => x.ItemTemplateId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<DbScheduleItem>()
            .Property(x => x.Name)
            .HasMaxLength(100);

        modelBuilder.Entity<DbScheduleItem>()
            .Property(x => x.FillIn)
            .HasMaxLength(200);

        modelBuilder.Entity<DbScheduleRunStep>()
            .HasOne(x => x.Schedule)
            .WithMany()
            .HasForeignKey(x => x.ScheduleId)
            .OnDelete(DeleteBehavior.Cascade);

        // Back reads the latest press on one track.
        modelBuilder.Entity<DbScheduleRunStep>()
            .HasIndex(x => new { x.ScheduleId, x.LaneId, x.At });

        modelBuilder.Entity<DbScheduleItemAdjustment>()
            .HasOne(x => x.Item)
            .WithMany(x => x.Adjustments)
            .HasForeignKey(x => x.ItemId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DbScheduleItemAdjustment>()
            .HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
