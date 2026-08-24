using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Data.Models.Sync.Enums;
using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Models;

namespace GSBC.ImpactKids.Grpc.Features.People.Sync.Descriptors;

public class PhoneNumberDescriptor : BaseFieldSyncDescriptor
{
    public override string        EntityType       => "Person";
    public override string        FieldName        => "PhoneNumber";
    public override SyncDirection DefaultDirection => SyncDirection.Bidirectional;

    public override string? GetFromApp(DbPerson person) => person.PhoneNumber;

    public override void SetOnApp(DbPerson person, string? value) =>
        person.PhoneNumber = string.IsNullOrWhiteSpace(value) ? null : value;

    public override string? GetFromElvanto(ElvantoPerson elv) =>
        string.IsNullOrWhiteSpace(elv.Mobile) ? (string.IsNullOrWhiteSpace(elv.Phone) ? null : elv.Phone) : elv.Mobile;

    public override void ApplyToElvantoRequest(ElvantoUpdatePersonRequest req, string? value) => req.Mobile = value;
}
