using GSBC.ImpactKids.Grpc.Services;

namespace GSBC.ImpactKids.Grpc.Features.Games.GameDisplayServices;

/// <summary>
/// "The scores moved", for the scoreboard wall display. The mechanism lives in
/// <see cref="DataChangeNotifier"/>; this type exists so the scoreboard and the pickup wall
/// wake on their own writes and not on each other's.
/// </summary>
public sealed class GameDataChangeNotifier : DataChangeNotifier;
