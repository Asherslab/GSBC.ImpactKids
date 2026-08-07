using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Games;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Games;
using GSBC.ImpactKids.Shared.Contracts.Services.Base;

namespace GSBC.ImpactKids.Shared.Contracts.Services.Features.Games;

[Service("gRPC/GSBC.ImpactKids.Games.PointRecords")]
public interface IGamePointRecordService
    : IBasicReadMultipleService<GamePointRecord>,
        ICreateService<CreateGamePointRecordRequest>,
        IBasicDeleteService<GamePointRecord>;
