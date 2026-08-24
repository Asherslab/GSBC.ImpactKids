using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Models;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices;

public partial class ElvantoService
{
    /// <summary>Fetches all Elvanto people as raw ElvantoPerson objects (no DB merge).</summary>
    public Task<List<ElvantoPerson>> GetAllPeopleAsync(CancellationToken token = default) =>
        RetrieveElvantoPeople(token);

    /// <summary>
    /// For a person-scoped sync: if the local person has an ElvantoId, fetch directly via getInfo.
    /// Otherwise fall back to searching the full list and returning matches by name.
    /// </summary>
    public async Task<List<ElvantoPerson>> GetPersonByIdOrSearchAsync(Guid localPersonId, CancellationToken token = default)
    {
        string? elvantoId = await db.People
            .Where(p => p.Id == localPersonId)
            .Select(p => p.ElvantoId)
            .FirstOrDefaultAsync(token);

        if (elvantoId is not null)
        {
            ElvantoPerson? person = await GetPersonInfoAsync(elvantoId, token);
            return person is null ? [] : [person];
        }

        // No link — return full list so the matcher can run
        return await RetrieveElvantoPeople(token);
    }

    /// <summary>
    /// For a family-scoped sync: fetch each linked family member by ID; add unlinked via full pull.
    /// </summary>
    public async Task<List<ElvantoPerson>> GetPeopleForFamilyAsync(Guid familyId, CancellationToken token = default)
    {
        List<string?> elvantoIds = await db.People
            .IgnoreQueryFilters()
            .Where(p => p.FamilyId == familyId)
            .Select(p => p.ElvantoId)
            .ToListAsync(token);

        bool hasUnlinked = elvantoIds.Any(id => id is null);

        List<ElvantoPerson> result = [];
        foreach (string? id in elvantoIds.Where(id => id is not null))
        {
            ElvantoPerson? p = await GetPersonInfoAsync(id!, token);
            if (p is not null) result.Add(p);
        }

        if (hasUnlinked)
        {
            // Pull full list so the matcher can find unlinked family members
            List<ElvantoPerson> all = await RetrieveElvantoPeople(token);
            result.AddRange(all.Where(elv => !result.Any(r => r.Id == elv.Id)));
        }

        return result;
    }

    private async Task<ElvantoPerson?> GetPersonInfoAsync(string elvantoId, CancellationToken token = default)
    {
        ElvantoGetPersonInfoResponse? resp = await SendMessage<ElvantoGetPersonInfoRequest, ElvantoGetPersonInfoResponse>(
            new ElvantoGetPersonInfoRequest { Id = elvantoId }, token);

        return resp?.Person;
    }
}
