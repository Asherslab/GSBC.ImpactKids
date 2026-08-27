using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Data.Models.Sync.Enums;
using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Models;

namespace GSBC.ImpactKids.Grpc.Features.People.Sync.Descriptors;

public class FirstNameDescriptor : BaseFieldSyncDescriptor
{
    public override string        EntityType       => "Person";
    public override string        FieldName        => "FirstName";
    public override SyncDirection DefaultDirection => SyncDirection.Bidirectional;

    public override string? GetFromApp(DbPerson person) => person.FirstName;
    public override bool SetOnApp(DbPerson person, string? value) => Assign(value, v => person.FirstName = v);
    public override string? GetFromElvanto(ElvantoPerson elv) => elv.FirstName;
    public override bool ApplyToElvantoRequest(ElvantoUpdatePersonRequest req, string? value) => Set(value, v => req.FirstName = v);
}