namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.People.Sync;

public enum ElvantoSyncMode
{
    Full    = 0,
    AppOnly = 1,
    DryRun  = 2
}

public enum ElvantoSyncScope
{
    All    = 0,
    Person = 1,
    Family = 2
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class SyncWithElvantoRequest
{
    public ElvantoSyncMode  Mode     { get; init; }
    public ElvantoSyncScope Scope    { get; init; }
    public Guid?            PersonId { get; init; } // set when Scope=Person
    public Guid?            FamilyId { get; init; } // set when Scope=Family
}
