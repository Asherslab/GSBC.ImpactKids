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
    public override bool    SetOnApp(DbPerson person, string? value) => Assign(value, v => person.Email = v);
    public override string? GetFromElvanto(ElvantoPerson elv) => string.IsNullOrWhiteSpace(elv.Email) ? null : elv.Email;
    public override bool    ApplyToElvantoRequest(ElvantoUpdatePersonRequest req, string? value) => Set(value, v => req.Email = v);
}
