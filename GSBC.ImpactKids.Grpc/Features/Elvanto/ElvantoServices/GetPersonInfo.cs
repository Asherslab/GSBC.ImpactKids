using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Models;

namespace GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices;

public partial class ElvantoService
{
    /// <summary>Fetches all Elvanto people as raw ElvantoPerson objects (no DB merge).</summary>
    public Task<List<ElvantoPerson>> GetAllPeopleAsync(CancellationToken token = default) =>
        RetrieveElvantoPeople(token);

    /// <summary>
    /// One person by Elvanto id.
    ///
    /// <b>This currently always returns null, and that is a known open defect, not a quirk to design
    /// around.</b> Elvanto's <c>people/getInfo</c> answers with <c>"person": [ { ... } ]</c> — an
    /// array — while <see cref="ElvantoGetPersonInfoResponse.Person"/> is declared as a single
    /// object, so the deserialize throws, the transport logs a warning and returns default, and the
    /// caller reads "no such person" off a clean HTTP 200. The fix is one line: make that property a
    /// list and take the first element.
    ///
    /// Two callers depend on it, both on the <c>family_id: "new"</c> path where Elvanto mints a
    /// household — <c>CreatePerson</c> when the create response omits <c>family_id</c>, and
    /// <c>UpdatePerson</c>, for which the read-back is the <i>only</i> source of that id. While it
    /// returns null, an edit that creates a household cannot learn which one, so no
    /// <c>ElvantoFamilyLinks</c> row is written for it and the next sibling asks for "new" again.
    ///
    /// It kept two other callers until 2026-08-27 — the person- and family-scoped fetches — and this
    /// defect is why both scopes silently processed nobody. The scopes were removed rather than
    /// repaired; this method stays because the two write-path callers are real.
    /// </summary>
    private async Task<ElvantoPerson?> GetPersonInfoAsync(string elvantoId, CancellationToken token = default)
    {
        ElvantoGetPersonInfoResponse? resp = await SendMessage<ElvantoGetPersonInfoRequest, ElvantoGetPersonInfoResponse>(
            new ElvantoGetPersonInfoRequest { Id = elvantoId }, token);

        return resp?.Person;
    }
}
