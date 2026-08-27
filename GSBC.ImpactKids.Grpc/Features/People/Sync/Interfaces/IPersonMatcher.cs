using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Models;
using GSBC.ImpactKids.Grpc.Features.People.Sync.Models;

namespace GSBC.ImpactKids.Grpc.Features.People.Sync.Interfaces;

public interface IPersonMatcher
{
    /// <summary>
    /// Finds the best local match for an Elvanto person who has no ElvantoId link yet.
    /// Returns null if no candidates exist at all.
    /// </summary>
    SyncMatchCandidate? FindBestMatch(ElvantoPerson elvantoPerson, IReadOnlyList<DbPerson> candidates);
}
