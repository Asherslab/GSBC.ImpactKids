using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Data.Models.Sync.Enums;
using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Models;

namespace GSBC.ImpactKids.Grpc.Features.People.Sync.Descriptors;

public class LastNameDescriptor : BaseFieldSyncDescriptor
{
    public override string        EntityType       => "Person";
    public override string        FieldName        => "LastName";
    public override SyncDirection DefaultDirection => SyncDirection.Bidirectional;

    public override string? GetFromApp(DbPerson person)       => person.LastName;
    public override void    SetOnApp(DbPerson person, string? value) => person.LastName = value ?? "";
    public override string? GetFromElvanto(ElvantoPerson elv) => elv.LastName;
    public override void    ApplyToElvantoRequest(ElvantoUpdatePersonRequest req, string? value) => req.LastName = value;
}
