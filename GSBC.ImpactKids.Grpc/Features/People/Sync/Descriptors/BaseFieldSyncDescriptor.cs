using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Data.Models.Sync.Enums;
using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Models;
using GSBC.ImpactKids.Grpc.Features.People.Sync.Interfaces;

namespace GSBC.ImpactKids.Grpc.Features.People.Sync.Descriptors;

public abstract class BaseFieldSyncDescriptor : IFieldSyncDescriptor
{
    public abstract string        EntityType       { get; }
    public abstract string        FieldName        { get; }
    public abstract SyncDirection DefaultDirection { get; }

    public virtual PrecedenceOnTie PrecedenceOnTie => PrecedenceOnTie.Elvanto;

    public abstract string? GetFromApp(DbPerson person);
    public abstract void    SetOnApp(DbPerson person, string? value);
    public abstract string? GetFromElvanto(ElvantoPerson elvantoPerson);
    public abstract bool    ApplyToElvantoRequest(ElvantoUpdatePersonRequest req, string? value);

    public virtual bool IsValidInboundValue(string? elvValue) => true;

    public virtual SyncSource FirstSyncPrecedence => SyncSource.Elvanto;

    public virtual string? MergeForFirstSync(string? appValue, string? elvValue) => appValue;

    /// <summary>
    /// Assigns an outbound value and reports that it was carried. Null is "nothing to say" and is
    /// declined; an empty string is a deliberate clear and is sent, because Elvanto ignores both a
    /// null and an omitted field while answering ok to each.
    /// </summary>
    protected static bool Set(string? value, Action<string> assign)
    {
        if (value is null) return false;
        assign(value);
        return true;
    }

    public string Hash(string? value) => SyncHash.Of(value);
}
