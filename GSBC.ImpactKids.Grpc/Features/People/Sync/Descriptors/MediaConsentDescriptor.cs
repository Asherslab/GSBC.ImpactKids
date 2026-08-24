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

    public override void SetOnApp(DbPerson person, string? value) =>
        person.MediaConsent = value ?? nameof(PeopleMediaConsent.NotRequested);

    public override string? GetFromElvanto(ElvantoPerson elv) =>
        elv.MediaConsent is null
            ? nameof(PeopleMediaConsent.NotRequested)
            : PeopleMediaConsentHelper.FromElvanto(elv.MediaConsent.Name).ToString();

    // NotRequested means "nothing collected" — never let it overwrite a real consent value in the app.
    public override bool IsValidInboundValue(string? elvValue) =>
        elvValue is not null && elvValue != nameof(PeopleMediaConsent.NotRequested);

    public override void ApplyToElvantoRequest(ElvantoUpdatePersonRequest req, string? value)
    {
        if (value is null) return;
        if (Enum.TryParse<PeopleMediaConsent>(value, out PeopleMediaConsent mc))
            req.MediaConsent = PeopleMediaConsentHelper.ToDisplay(mc);
    }
}
