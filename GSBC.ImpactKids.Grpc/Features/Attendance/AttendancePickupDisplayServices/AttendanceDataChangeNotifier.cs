using GSBC.ImpactKids.Grpc.Services;

namespace GSBC.ImpactKids.Grpc.Features.Attendance.AttendancePickupDisplayServices;

/// <summary>
/// "An attendance record was written", for the pickup wall display. The mechanism lives in
/// <see cref="DataChangeNotifier"/>; this type exists so the pickup wall and the scoreboard
/// wake on their own writes and not on each other's.
/// </summary>
public sealed class AttendanceDataChangeNotifier : DataChangeNotifier;
