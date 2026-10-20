using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Songs;
using GSBC.ImpactKids.Shared.Contracts.Services.Base;

namespace GSBC.ImpactKids.Shared.Contracts.Services.Features.Songs;

/// <summary>
/// The song library, for leaders. Read only: songs are written by the ProPresenter agent through
/// <c>IProPresenterAgentService.SyncSongs</c>, and nowhere else.
/// </summary>
[Service("gRPC/GSBC.ImpactKids.Songs")]
public interface ISongService : IBasicReadMultipleService<Song>;
