using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scripture;
using GSBC.ImpactKids.Shared.Contracts.Services.Base;

namespace GSBC.ImpactKids.Shared.Contracts.Services.Features.Scripture;

[Service("gRPC/GSBC.ImpactKids.Bible")]
public interface IBibleService : IBasicReadMultipleService<BibleVerse>;