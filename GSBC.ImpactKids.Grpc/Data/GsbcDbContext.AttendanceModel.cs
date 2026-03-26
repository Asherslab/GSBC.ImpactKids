using GSBC.ImpactKids.Grpc.Data.Models.Attendance;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Data;

public partial class GsbcDbContext
{
    // Attendance \\
    public required DbSet<DbAttendanceRecord>     AttendanceRecords     { get; set; }
    public required DbSet<DbAttendanceItemType>   AttendanceItemTypes   { get; set; }
    public required DbSet<DbAttendanceItemRecord> AttendanceItemRecords { get; set; }

    private static void BuildAttendanceModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DbAttendanceRecord>()
            .HasOne(x => x.Person)
            .WithMany()
            .HasForeignKey(x => x.PersonId);

        modelBuilder.Entity<DbAttendanceRecord>()
            .HasOne(x => x.Service)
            .WithMany()
            .HasForeignKey(x => x.ServiceId);

        modelBuilder.Entity<DbAttendanceRecord>()
            .HasOne(x => x.SignedInUser)
            .WithMany()
            .HasForeignKey(x => x.SignedInUserId);

        modelBuilder.Entity<DbAttendanceRecord>()
            .HasOne(x => x.SignedOutUser)
            .WithMany()
            .HasForeignKey(x => x.SignedOutUserId);

        modelBuilder.Entity<DbAttendanceItemRecord>()
            .HasOne(x => x.AttendanceRecord)
            .WithMany()
            .HasForeignKey(x => x.AttendanceRecordId);

        modelBuilder.Entity<DbAttendanceItemRecord>()
            .HasOne(x => x.AttendanceItemType)
            .WithMany()
            .HasForeignKey(x => x.AttendanceItemTypeId);
    }
}