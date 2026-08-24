using System.Security.Cryptography;
using System.Text;
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

    public abstract string? GetFromApp(DbPerson person);
    public abstract void    SetOnApp(DbPerson person, string? value);
    public abstract string? GetFromElvanto(ElvantoPerson elvantoPerson);
    public abstract void    ApplyToElvantoRequest(ElvantoUpdatePersonRequest req, string? value);

    public virtual bool IsValidInboundValue(string? elvValue) => true;

    public string Hash(string? value)
    {
        if (value is null) return "null";
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value.Trim().ToLowerInvariant()));
        return Convert.ToHexString(bytes)[..16];
    }
}
