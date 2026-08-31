using GSBC.ImpactKids.Grpc.Features.People.Sync.Services;
using CreateGate = GSBC.ImpactKids.Grpc.Features.People.Sync.Services.ElvantoPersonSyncService.CreateGate;

namespace GSBC.ImpactKids.Grpc.Tests.Sync;

/// <summary>
/// One run must not both link a person and create them.
///
/// It did. Lydia and Julia Agatep each came out of a single decide with a <c>LinkPerson</c> row at
/// <c>Confidence=100:ExactNameAndDob</c> and a <c>CreateInElvanto</c> row, because the create loop
/// asked <c>DbPerson.ElvantoId</c> — which Decide never sets, Apply does — and so read a person the
/// matcher had just paired as nobody's. The link removed them from <c>UnlinkedApp</c>, but the
/// create loop reads <c>AppPeople</c>, so the removal bought nothing.
///
/// Apply masked it: it runs the link loop first, saves, then skips any create whose person now has
/// an <c>ElvantoId</c>. That is not a rescue to keep — a link marked Stale (the Elvanto record
/// changed after the plan was decided, or left the roll) leaves <c>ElvantoId</c> null and the create
/// goes through, duplicating in Elvanto the record the person was matched to.
///
/// Pure local logic: no Elvanto call, no database.
/// </summary>
public class CreateGateTests
{
    private static readonly Guid Person = new("22222222-2222-2222-2222-222222222222");

    private static HashSet<Guid> None => [];
    private static HashSet<Guid> Just(Guid id) => [id];

    [Fact]
    public void APersonLinkedByThisRunIsNotAlsoCreatedInElvanto()
    {
        CreateGate gate = ElvantoPersonSyncService.GateForCreate(
            Person,
            linkedThisRunIds: Just(Person),
            reviewCandidateIds: None,
            awaitingReviewIds: None,
            deniedPairIds: None);

        Assert.Equal(CreateGate.LinkedThisRun, gate);
    }

    /// <summary>
    /// The reason the link is asked first. A person can carry an unanswered review from an earlier
    /// run and still be linked by this one — an approved review is exactly that — and reporting them
    /// as held would count settled work as outstanding.
    /// </summary>
    [Fact]
    public void ALinkBeatsAnUnansweredReviewFromAnEarlierRun()
    {
        CreateGate gate = ElvantoPersonSyncService.GateForCreate(
            Person,
            linkedThisRunIds: Just(Person),
            reviewCandidateIds: None,
            awaitingReviewIds: Just(Person),
            deniedPairIds: None);

        Assert.Equal(CreateGate.LinkedThisRun, gate);
    }

    [Fact]
    public void AnUnclaimedPersonIsStillCreated()
    {
        CreateGate gate = ElvantoPersonSyncService.GateForCreate(
            Person, None, None, None, None);

        Assert.Equal(CreateGate.Proceed, gate);
    }

    [Fact]
    public void AReviewCandidateRaisedThisRunIsHeld()
    {
        CreateGate gate = ElvantoPersonSyncService.GateForCreate(
            Person,
            linkedThisRunIds: None,
            reviewCandidateIds: Just(Person),
            awaitingReviewIds: None,
            deniedPairIds: None);

        Assert.Equal(CreateGate.ReviewCandidate, gate);
    }

    [Fact]
    public void AnUnansweredReviewHoldsTheCreate()
    {
        CreateGate gate = ElvantoPersonSyncService.GateForCreate(
            Person,
            linkedThisRunIds: None,
            reviewCandidateIds: None,
            awaitingReviewIds: Just(Person),
            deniedPairIds: None);

        Assert.Equal(CreateGate.AwaitingReview, gate);
    }

    /// <summary>
    /// Denying a low-confidence match says these are two different people, which is the case where
    /// the app person genuinely needs creating. Suppressing the create anyway made a denial
    /// permanent.
    /// </summary>
    [Fact]
    public void ADenialReleasesTheCreate()
    {
        CreateGate gate = ElvantoPersonSyncService.GateForCreate(
            Person,
            linkedThisRunIds: None,
            reviewCandidateIds: None,
            awaitingReviewIds: Just(Person),
            deniedPairIds: Just(Person));

        Assert.Equal(CreateGate.Proceed, gate);
    }
}
