namespace GSBC.ImpactKids.Grpc.Data.Models.Sync.Enums;

/// <summary>
/// Where a <see cref="DbElvantoFamilyLink"/> came from. Worth storing for the first time two rows
/// disagree and someone has to work out which of them to believe.
/// </summary>
public enum ElvantoFamilyLinkSource
{
    /// <summary>Recovered from the field snapshots the engine had already written, at bootstrap.</summary>
    Seeded,

    /// <summary>Elvanto named a household on a person and this app had no row for it yet.</summary>
    Observed,

    /// <summary>Elvanto minted the household when this app created a person or moved one.</summary>
    CreatedInElvanto
}
