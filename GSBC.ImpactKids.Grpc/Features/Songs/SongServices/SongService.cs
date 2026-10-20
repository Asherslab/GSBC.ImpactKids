using GSBC.ImpactKids.Grpc.Conversion;
using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Data.Models.Songs;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Songs;
using GSBC.ImpactKids.Shared.Contracts.Services.Features.Songs;

namespace GSBC.ImpactKids.Grpc.Features.Songs.SongServices;

/// <summary>
/// The song library, read only, for leaders - see <see cref="ISongService"/>. Leader only by carrying
/// no authorization attribute. Songs are written by the ProPresenter agent, never here.
/// </summary>
public partial class SongService(
    GsbcDbContext              db,
    IConverter<DbSong, Song>   converter
) : ISongService;
