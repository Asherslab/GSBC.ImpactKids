using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Games;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Games;
using GSBC.ImpactKids.Shared.Contracts.Services.Base;

namespace GSBC.ImpactKids.Shared.Contracts.Services.Features.Games;

[Service("gRPC/GSBC.ImpactKids.Games.Boards")]
public interface IGameBoardService
    : IBasicReadMultipleService<GameBoard>,
        // Create upserts by service - there is only ever one board per service.
        ICreateService<UpsertGameBoardRequest>;
