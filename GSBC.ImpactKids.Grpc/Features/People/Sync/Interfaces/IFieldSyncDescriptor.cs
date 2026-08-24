using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Data.Models.Sync.Enums;
using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Models;

namespace GSBC.ImpactKids.Grpc.Features.People.Sync.Interfaces;

public interface IFieldSyncDescriptor
{
    string        EntityType        { get; }
    string        FieldName         { get; }
    SyncDirection DefaultDirection  { get; }

    string? GetFromApp(DbPerson person);
    void    SetOnApp(DbPerson person, string? value);

    string? GetFromElvanto(ElvantoPerson elvantoPerson);
    void    ApplyToElvantoRequest(ElvantoUpdatePersonRequest req, string? value);

    string Hash(string? value);

    /// <summary>
    /// Returns false when the Elvanto value is semantically empty for this field
    /// (e.g. a consent state that means "nothing set") and should never drive an
    /// inbound update or win a conflict against a real app value.
    /// </summary>
    bool IsValidInboundValue(string? elvValue) => true;
}
