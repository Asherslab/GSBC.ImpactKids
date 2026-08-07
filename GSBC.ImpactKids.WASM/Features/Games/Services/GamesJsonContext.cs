using System.Text.Json.Serialization;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Games;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Games;

namespace GSBC.ImpactKids.WASM.Features.Games.Services;

/// <summary>
/// Source generated so the browser storage round trip keeps working under trimming.
/// </summary>
[JsonSerializable(typeof(List<GamePointRecord>))]
[JsonSerializable(typeof(List<CreateGamePointRecordRequest>))]
[JsonSerializable(typeof(List<GameBoard>))]
[JsonSerializable(typeof(List<UpsertGameBoardRequest>))]
[JsonSerializable(typeof(List<Guid>))]
internal partial class GamesJsonContext : JsonSerializerContext;
