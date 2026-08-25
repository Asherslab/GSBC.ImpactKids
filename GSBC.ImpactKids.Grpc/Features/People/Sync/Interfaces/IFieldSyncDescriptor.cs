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

    /// <summary>
    /// Which side wins the first time a field is seen, when there is no snapshot and so no
    /// trustworthy "changed at" on either side. Elvanto by default, which preserves the
    /// existing behaviour for every field that does not say otherwise.
    /// </summary>
    SyncSource FirstSyncPrecedence => SyncSource.Elvanto;

    /// <summary>
    /// The value to push when the app wins a first sync. Defaults to the app value outright,
    /// which is a plain overwrite. A descriptor whose Elvanto side is free text can override
    /// this to carry across anything the app does not already say, so a first sync cannot
    /// silently delete something a person typed into Elvanto years ago.
    /// </summary>
    string? MergeForFirstSync(string? appValue, string? elvValue) => appValue;
}
