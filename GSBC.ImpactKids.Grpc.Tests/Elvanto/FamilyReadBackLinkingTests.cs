using GSBC.ImpactKids.Grpc.Data.Models.Sync;
using GSBC.ImpactKids.Grpc.Data.Models.Sync.Enums;
using GSBC.ImpactKids.Grpc.Features.People.Sync.Models;

namespace GSBC.ImpactKids.Grpc.Tests.Elvanto;

/// <summary>
/// What the <c>people/getInfo</c> fix newly makes reachable.
///
/// That method returned null for every call, so the family read-back on the <c>family_id: "new"</c>
/// path always produced null and <c>LinkFamily</c> was never invoked from either writer. Fixing the
/// response shape turns those branches on for the first time — <c>CreatePerson</c> line 143 and
/// <c>UpdatePerson</c> line 46 — and both funnel into <see cref="SyncFamilyLinks.Record"/>.
///
/// So the risk of that fix is not in the parsing, it is here: a value now arrives where only null
/// ever did. These tests pin the guards at that choke point, in particular the blank Elvanto
/// household id, which <c>Apply</c>'s <c>is not null</c> check would happily pass through — Elvanto
/// returns an empty string for a person with no household, and the writers do not filter it.
///
/// Pure local logic: no Elvanto call, no database.
/// </summary>
public class FamilyReadBackLinkingTests
{
    private static readonly Guid LocalFamily = new("11111111-1111-1111-1111-111111111111");

    private static SyncFamilyLinks Empty() => new([]);

    private static SyncFamilyLinks WithRow(Guid local, string elvanto) => new([
        new DbElvantoFamilyLink
        {
            Id              = Guid.NewGuid(),
            LocalFamilyId   = local,
            ElvantoFamilyId = elvanto,
            Source          = ElvantoFamilyLinkSource.Observed,
            LinkedAtUtc     = DateTimeOffset.UnixEpoch
        }
    ]);

    [Fact]
    public void AReadBackHouseholdIsRememberedInBothDirections()
    {
        SyncFamilyLinks links = Empty();

        DbElvantoFamilyLink? row = links.Record(LocalFamily, "4671", ElvantoFamilyLinkSource.CreatedInElvanto);

        Assert.NotNull(row);
        Assert.Equal(ElvantoFamilyLinkSource.CreatedInElvanto, row.Source);
        Assert.Equal(LocalFamily, links.LocalFor("4671"));
        Assert.Equal("4671", links.ElvantoFor(LocalFamily));
        Assert.Single(links.Added);
    }

    /// <summary>
    /// The case the fix could plausibly have introduced. Elvanto answers with an empty
    /// <c>family_id</c> for a person it has no household for, and both writers pass whatever the
    /// read-back holds straight to <c>Apply</c>, whose only test is <c>is not null</c> — so <c>""</c>
    /// reaches Record. It must not become a row: an empty household id paired to a real local family
    /// would be a pairing to nothing, and unique on both sides, so it would also block the real
    /// household from ever being recorded.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ABlankHouseholdIdIsNotAHousehold(string elvantoFamilyId)
    {
        SyncFamilyLinks links = Empty();

        Assert.Null(links.Record(LocalFamily, elvantoFamilyId, ElvantoFamilyLinkSource.CreatedInElvanto));
        Assert.Empty(links.Added);
        Assert.Null(links.ElvantoFor(LocalFamily));
    }

    /// <summary>
    /// Guid.Empty is "no family recorded" and the bucket is 412 unrelated people sharing one Guid.
    /// A create for someone in either state still asks Elvanto for a new household, so the read-back
    /// now returns one — and pairing it to these would declare a non-family a family.
    /// </summary>
    [Fact]
    public void NeitherEmptyNorTheBucketMayBePaired()
    {
        SyncFamilyLinks links = Empty();

        Assert.Null(links.Record(Guid.Empty, "4671", ElvantoFamilyLinkSource.CreatedInElvanto));
        Assert.Null(links.Record(SyncFamilyLinks.UngroupedBucket, "4671", ElvantoFamilyLinkSource.CreatedInElvanto));
        Assert.Empty(links.Added);
        Assert.Null(links.LocalFor("4671"));
    }

    /// <summary>
    /// Both sides are unique, and a second apply in the same run can reach Record for a household
    /// already recorded. It must decline rather than add a duplicate the database would reject.
    /// </summary>
    [Fact]
    public void AnAlreadyPairedSideIsDeclinedRatherThanDuplicated()
    {
        Guid            otherLocal = new("22222222-2222-2222-2222-222222222222");
        SyncFamilyLinks links      = WithRow(LocalFamily, "4671");

        // the household is spoken for
        Assert.Null(links.Record(otherLocal, "4671", ElvantoFamilyLinkSource.CreatedInElvanto));
        // the local family is spoken for
        Assert.Null(links.Record(LocalFamily, "9999", ElvantoFamilyLinkSource.CreatedInElvanto));

        Assert.Empty(links.Added);
        Assert.Equal(LocalFamily, links.LocalFor("4671"));
        Assert.Equal("4671", links.ElvantoFor(LocalFamily));
    }

    /// <summary>
    /// The reason the read-back matters at all: the second member of a household must join the first
    /// rather than ask for another "new". Once one member's household is recorded, the next lookup
    /// answers, which is what <c>Apply</c> consults before deciding to ask for a new one.
    /// </summary>
    [Fact]
    public void ASiblingFindsTheHouseholdTheFirstMemberRecorded()
    {
        SyncFamilyLinks links = Empty();

        links.Record(LocalFamily, "4671", ElvantoFamilyLinkSource.CreatedInElvanto);

        Assert.Equal("4671", links.ElvantoFor(LocalFamily));
    }
}
