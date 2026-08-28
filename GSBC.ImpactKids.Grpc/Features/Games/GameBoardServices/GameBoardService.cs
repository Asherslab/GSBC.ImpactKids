using GSBC.ImpactKids.Grpc.Conversion;
using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Data.Models.Games;
using GSBC.ImpactKids.Grpc.Services;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Games;
using GSBC.ImpactKids.Shared.Contracts.Services.Features.Games;

namespace GSBC.ImpactKids.Grpc.Features.Games.GameBoardServices;

public partial class GameBoardService(
    GsbcDbContext                      db,
    IEventService<GameBoard>           eventService,
    IConverter<DbGameBoard, GameBoard> converter
) : IGameBoardService;
