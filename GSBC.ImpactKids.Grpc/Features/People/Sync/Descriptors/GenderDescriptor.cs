using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Data.Models.Sync.Enums;
using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Models;
using PeopleGender = GSBC.ImpactKids.Shared.Contracts.Entities.Features.People.Gender;

namespace GSBC.ImpactKids.Grpc.Features.People.Sync.Descriptors;

/// <summary>
/// Gender, both ways.
///
/// <b>Bidirectional plans zero outbound writes at seed time</b>, which is worth knowing before
/// anyone "fixes" this to InboundOnly. Every app-side gender starts null, so
/// <c>FieldReconciler.Decide</c> finds no base row and falls to <c>DecideFirstSync</c>, where
/// <c>appHasSomethingToSay</c> is false for every person — the two branches that produce outbound
/// rows are unreachable. Elvanto holding "Male" lands inbound via FirstSync:ElvantoPrecedence;
/// Elvanto holding "" settles as Match:NeitherSideSaysAnything, no row and no noise. So "take
/// Elvanto's value when the app holds null" needs no special casing here: it is already what the
/// reconciler does.
///
/// Outbound rows appear later, and from one source only — a leader filling in a gender for a child
/// Elvanto has blank, read as an app-side change against the settled base. That is the end state
/// this field exists for.
/// </summary>
public class GenderDescriptor : BaseFieldSyncDescriptor
{
    public override string        EntityType       => "Person";
    public override string        FieldName        => "Gender";
    public override SyncDirection DefaultDirection => SyncDirection.Bidirectional;

    public override string? GetFromApp(DbPerson person) => person.Gender;

    public override bool SetOnApp(DbPerson person, string? value) =>
        Assign(value, v => person.Gender = v);

    /// <summary>
    /// Elvanto returns exactly "Male", "Female" or "" — measured across a full roll. The blank falls
    /// to the base <see cref="BaseFieldSyncDescriptor.IsValidInboundValue"/>, which already refuses
    /// it, so Elvanto holding nothing can never clear a gender a leader typed here.
    /// </summary>
    public override string? GetFromElvanto(ElvantoPerson elv) => elv.Gender;

    /// <summary>
    /// Only ever one of the two real answers. Anything else is not a gender, and pushing it would be
    /// refused by Elvanto anyway — declining is correct, and reporting a decline as a push is not.
    /// There is deliberately no clear: null here means the app has nothing to say, not that Elvanto
    /// should be emptied.
    /// </summary>
    public override bool ApplyToElvantoRequest(ElvantoUpdatePersonRequest req, string? value)
    {
        if (value is null || !Enum.TryParse(value, out PeopleGender gender))
            return false;

        return Set(gender.ToString(), v => req.Gender = v);
    }
}
