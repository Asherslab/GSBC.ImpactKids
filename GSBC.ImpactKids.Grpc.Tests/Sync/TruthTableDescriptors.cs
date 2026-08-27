using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Data.Models.Sync.Enums;
using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Models;
using GSBC.ImpactKids.Grpc.Features.People.Sync.Descriptors;

namespace GSBC.ImpactKids.Grpc.Tests.Sync;

/// <summary>
/// A descriptor whose only job is to be a field. The reconciler asks a descriptor four things -
/// its name, whether an Elvanto value says anything, which side wins a first sync, and how to merge
/// one - so those four are what a test needs to vary.
/// </summary>
public sealed class TruthTableDescriptor : BaseFieldSyncDescriptor
{
    public override string        EntityType       => "Person";
    public override string        FieldName        => "TestField";
    public override SyncDirection DefaultDirection => Direction;

    public Func<string?, bool> Usable           { get; init; } = _ => true;
    public SyncSource          FirstSync        { get; init; } = SyncSource.Elvanto;
    public bool                MergeOnFirstSync { get; init; }
    public SyncDirection       Direction        { get; init; } = SyncDirection.Bidirectional;
    public PrecedenceOnTie     Tie              { get; init; } = PrecedenceOnTie.Elvanto;

    public override bool            IsValidInboundValue(string? elvValue) => Usable(elvValue);
    public override SyncSource      FirstSyncPrecedence                   => FirstSync;
    public override PrecedenceOnTie PrecedenceOnTie                       => Tie;

    public override string? MergeForFirstSync(string? appValue, string? elvValue) =>
        MergeOnFirstSync && !string.IsNullOrWhiteSpace(elvValue)
            ? $"{appValue}\n{elvValue}"
            : appValue;

    public override string? GetFromApp(DbPerson person)               => null;
    public override bool    SetOnApp(DbPerson person, string? value)  => true;
    public override string? GetFromElvanto(ElvantoPerson elv)         => null;

    public override bool ApplyToElvantoRequest(ElvantoUpdatePersonRequest req, string? value) =>
        Set(value, v => req.Email = v);
}
