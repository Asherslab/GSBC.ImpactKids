using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Data.Models.Sync.Enums;
using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Models;

namespace GSBC.ImpactKids.Grpc.Features.People.Sync.Descriptors;

public class EmailDescriptor : BaseFieldSyncDescriptor
{
    public override string        EntityType       => "Person";
    public override string        FieldName        => "Email";
    public override SyncDirection DefaultDirection => SyncDirection.Bidirectional;

    public override string? GetFromApp(DbPerson person)       => person.Email;
    public override void    SetOnApp(DbPerson person, string? value) => person.Email = string.IsNullOrWhiteSpace(value) ? null : value;
    public override string? GetFromElvanto(ElvantoPerson elv) => string.IsNullOrWhiteSpace(elv.Email) ? null : elv.Email;
    public override void    ApplyToElvantoRequest(ElvantoUpdatePersonRequest req, string? value) => req.Email = value;
}
