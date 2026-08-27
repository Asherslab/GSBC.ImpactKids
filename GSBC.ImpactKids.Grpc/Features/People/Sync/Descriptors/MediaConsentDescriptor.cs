using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Data.Models.Sync.Enums;
using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Models;
using PeopleMediaConsent = GSBC.ImpactKids.Shared.Contracts.Entities.Features.People.MediaConsent;
using PeopleMediaConsentHelper = GSBC.ImpactKids.Shared.Contracts.Entities.Features.People.MediaConsentHelper;

namespace GSBC.ImpactKids.Grpc.Features.People.Sync.Descriptors;

public class MediaConsentDescriptor : BaseFieldSyncDescriptor
{
    public override string        EntityType       => "Person";
    public override string        FieldName        => "MediaConsent";
    public override SyncDirection DefaultDirection => SyncDirection.Bidirectional;

    public override string? GetFromApp(DbPerson person) => person.MediaConsent;

    public override bool SetOnApp(DbPerson person, string? value) =>
        Assign(value, v => person.MediaConsent = v);

    public override string? GetFromElvanto(ElvantoPerson elv) =>
        elv.MediaConsent is null
            ? nameof(PeopleMediaConsent.NotRequested)
            : PeopleMediaConsentHelper.FromElvanto(elv.MediaConsent.Name).ToString();

    // NotRequested means "nothing collected" — never let it overwrite a real consent value in the
    // app. Blank says the same thing and is refused by the base, so this adds to that rule rather
    // than replacing it: written as its own null check it let "" through, which is a clear.
    public override bool IsValidInboundValue(string? elvValue) =>
        base.IsValidInboundValue(elvValue) && elvValue != nameof(PeopleMediaConsent.NotRequested);

    public override bool ApplyToElvantoRequest(ElvantoUpdatePersonRequest req, string? value)
    {
        // An empty string is the only thing Elvanto accepts as a clear on this select field; the
        // option-id lookup passes an unknown name straight through, so "" reaches the wire as "".
        if (value is not null && value.Length == 0)
            return Set(value, v => req.MediaConsent = v);

        // Anything that is not one of the four options is not a consent answer, and pushing it
        // would be refused. Declining is correct; reporting that as a successful push was not.
        if (value is null || !Enum.TryParse(value, out PeopleMediaConsent mc))
            return false;

        return Set(PeopleMediaConsentHelper.ToDisplay(mc), v => req.MediaConsent = v);
    }
}
