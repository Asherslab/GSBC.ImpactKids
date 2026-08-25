using GSBC.ImpactKids.Grpc.Data.Models.Sync.Enums;
using GSBC.ImpactKids.Grpc.Features.People.Sync.Interfaces;
using GSBC.ImpactKids.Grpc.Features.People.Sync.Models;
using GSBC.ImpactKids.Grpc.Features.People.Sync.Services;

namespace GSBC.ImpactKids.Grpc.Tests.Sync;

/// <summary>
/// The nineteen rows of the truth table in <c>docs/work/2026-08-elvanto-sync-review.md</c>, which
/// enumerates every path through the field decision. Nine of them used to end in no action, no audit
/// row and no counter; this asserts that none of them does now.
///
/// The row numbers are the doc's. Where a row's outcome deliberately changed, the test says what it
/// used to do — a row here is a claim about the old bug as much as about the new behaviour.
///
/// The old columns map straight onto the new inputs: <c>S</c> (a snapshot exists) is "there is a
/// base", <c>EH</c> is "Elvanto's leg moved", <c>AT</c> is "the app's leg moved", <c>V</c> is
/// <c>IsValidInboundValue</c>, and <c>D</c> is the configured direction.
/// </summary>
public class FieldReconcilerTruthTableTests
{
    private static readonly IFieldReconciler Reconciler = new FieldReconciler(new ConflictResolver());

    /// <summary>
    /// Direction and tie-breaking now live on the descriptor, which is the only authority on them.
    /// A test that wants a direction makes a descriptor that declares it.
    /// </summary>
    private static TruthTableDescriptor With(
        TruthTableDescriptor desc,
        SyncDirection        direction,
        PrecedenceOnTie      tie = PrecedenceOnTie.Elvanto) =>
        new()
        {
            Usable           = desc.Usable,
            FirstSync        = desc.FirstSync,
            MergeOnFirstSync = desc.MergeOnFirstSync,
            Direction        = direction,
            Tie              = tie
        };

    /// <summary>
    /// Builds a comparison from the app and Elvanto values plus, optionally, what the base holds.
    /// Passing no base is the "no snapshot" half of the table, and is also every row in a database
    /// whose app leg was never backfilled.
    /// </summary>
    private static FieldComparison Compare(
        TruthTableDescriptor desc,
        string?              appValue,
        string?              elvValue,
        string?              baseApp          = null,
        string?              baseElvanto      = null,
        bool                 hasBase          = false,
        DateTimeOffset?      appChangedAt     = null,
        DateTimeOffset?      elvantoChangedAt = null) => new()
    {
        AppValue           = appValue,
        ElvantoValue       = elvValue,
        AppHash            = desc.Hash(appValue),
        ElvantoHash        = desc.Hash(elvValue),
        BaseAppHash        = hasBase ? desc.Hash(baseApp) : null,
        BaseElvantoHash    = hasBase ? desc.Hash(baseElvanto) : null,
        ElvantoValueUsable = desc.IsValidInboundValue(elvValue),
        AppChangedAt       = appChangedAt,
        ElvantoChangedAt   = elvantoChangedAt
    };

    private static readonly TruthTableDescriptor Plain = new();

    /// <summary>"Not Requested" and "None" mean nothing was collected, and must never overwrite a real value.</summary>
    private static readonly TruthTableDescriptor SaysNothingIsUnusable =
        new() { Usable = v => !string.IsNullOrWhiteSpace(v) && v != "None" };

    /// <summary>The medical/allergy box: the app is the system of record on a first sync, and it merges.</summary>
    private static readonly TruthTableDescriptor AppWinsFirstSync =
        new() { FirstSync = SyncSource.App, MergeOnFirstSync = true };

    // ---------------------------------------------------------------- row 1

    [Fact]
    public void Row1_DisabledDirection_SkipsAndLeavesTheBaseAlone()
    {
        // Was: nothing happened, no audit row, and the snapshot advanced anyway - so a pending app
        // change on a disabled field was consumed by a run that had decided not to look at it.
        FieldDecision d = Reconciler.Decide(
                With(Plain, SyncDirection.Disabled),
                Compare(Plain, "app", "elvanto", "app", "app", hasBase: true));

        Assert.Equal(FieldDecisionKind.Skipped, d.Kind);
        Assert.Equal("Direction:Disabled", d.Reason);
    }

    // ---------------------------------------------------------------- row 2

    [Fact]
    public void Row2_SidesAlreadyAgree_WritesTheBase()
    {
        FieldDecision d = Reconciler.Decide(
                With(Plain, SyncDirection.Bidirectional),
                Compare(Plain, "same", "same"));

        Assert.Equal(FieldDecisionKind.Agreed, d.Kind);
    }

    [Fact]
    public void Row2_AgreementIsCheckedBeforeDirection_ButAfterDisabled()
    {
        // An agreed field on an InboundOnly config still settles: there is nothing to refuse.
        Assert.Equal(
            FieldDecisionKind.Agreed,
            Reconciler.Decide(
                With(Plain, SyncDirection.InboundOnly),
                Compare(Plain, "same", "same")).Kind);
    }

    // ---------------------------------------------------------------- row 3

    [Fact]
    public void Row3_BaseSaysNeitherSideMovedButTheyDiffer_IsReported()
    {
        // Was: a bare continue. The single most durable silent divergence - identical on every run,
        // forever, with no audit row, which reads exactly like success.
        FieldDecision d = Reconciler.Decide(
                With(Plain, SyncDirection.Bidirectional),
                Compare(Plain, "app", "elvanto", baseApp: "app", baseElvanto: "elvanto", hasBase: true));

        Assert.Equal(FieldDecisionKind.Diverged, d.Kind);
        Assert.Equal("BaseDisagreesWithBothSides", d.Reason);
    }

    // ---------------------------------------------------------------- row 4

    [Fact]
    public void Row4_NoBaseAndElvantoSaysNothing_PushesTheAppValue()
    {
        // Was: nothing, plus a snapshot created with LastSeenAt = now, which burned appChanged for
        // every future run. This row is the restored-dump backlog: an app value Elvanto never had.
        FieldDecision d = Reconciler.Decide(
                With(SaysNothingIsUnusable, SyncDirection.Bidirectional),
                Compare(SaysNothingIsUnusable, "0435862120", null));

        Assert.Equal(FieldDecisionKind.Outbound, d.Kind);
        Assert.Equal("FirstSync:ElvantoHasNothing", d.Reason);
        Assert.Equal("0435862120", d.Value);
    }

    // ---------------------------------------------------------------- rows 5, 6

    [Fact]
    public void Row5_NoBaseAndElvantoHasAUsableValue_IsInbound()
    {
        FieldDecision d = Reconciler.Decide(
                With(Plain, SyncDirection.Bidirectional),
                Compare(Plain, null, "elvanto"));

        Assert.Equal(FieldDecisionKind.Inbound, d.Kind);
        Assert.Equal("FirstSync:ElvantoPrecedence", d.Reason);
    }

    [Fact]
    public void Row6_SameButOutboundOnly_IsARefusalWithAName()
    {
        // Was: silent. Nothing matched any branch and nothing was written.
        FieldDecision d = Reconciler.Decide(
                With(Plain, SyncDirection.OutboundOnly),
                Compare(Plain, null, "elvanto"));

        Assert.Equal(FieldDecisionKind.Diverged, d.Kind);
        Assert.Equal("DirectionRefused:OutboundOnly:Inbound:FirstSync:ElvantoPrecedence", d.Reason);
    }

    // ---------------------------------------------------------------- rows 7, 8, 9

    [Fact]
    public void Row7_ElvantoMovedAlone_IsInbound()
    {
        FieldDecision d = Reconciler.Decide(
                With(Plain, SyncDirection.Bidirectional),
                Compare(Plain, "app", "moved", baseApp: "app", baseElvanto: "app", hasBase: true));

        Assert.Equal(FieldDecisionKind.Inbound, d.Kind);
        Assert.Equal("ElvantoChangedAlone", d.Reason);
        Assert.Equal("moved", d.Value);
    }

    [Fact]
    public void Row8_ElvantoMovedAloneOnAnOutboundOnlyField_IsReportedNotMarkedSeen()
    {
        // Was: Elvanto's change was discarded AND the snapshot advanced, so the divergence was
        // recorded as having been seen. The caller must not settle a base on a Diverged decision.
        FieldDecision d = Reconciler.Decide(
                With(Plain, SyncDirection.OutboundOnly),
                Compare(Plain, "app", "moved", baseApp: "app", baseElvanto: "app", hasBase: true));

        Assert.Equal(FieldDecisionKind.Diverged, d.Kind);
        Assert.Contains("DirectionRefused:OutboundOnly:Inbound", d.Reason);
    }

    [Fact]
    public void Row9_ElvantoClearedTheFieldToSomethingMeaningless_DoesNotWipeTheApp()
    {
        // Was: the app kept its value silently and the snapshot advanced anyway.
        FieldDecision d = Reconciler.Decide(
                With(SaysNothingIsUnusable, SyncDirection.Bidirectional),
                Compare(SaysNothingIsUnusable, "Yes", "None", baseApp: "Yes", baseElvanto: "Yes", hasBase: true));

        Assert.Equal(FieldDecisionKind.Diverged, d.Kind);
        Assert.Equal("InvalidInboundValue:ElvantoChangedAlone", d.Reason);
    }

    // ---------------------------------------------------------------- row 10

    [Fact]
    public void Row10_AppMovedAlone_IsOutbound()
    {
        FieldDecision d = Reconciler.Decide(
                With(Plain, SyncDirection.Bidirectional),
                Compare(Plain, "new", "elvanto", baseApp: "elvanto", baseElvanto: "elvanto", hasBase: true));

        Assert.Equal(FieldDecisionKind.Outbound, d.Kind);
        Assert.Equal("AppChangedAlone", d.Reason);
        Assert.Equal("new", d.Value);
    }

    [Fact]
    public void Row10_AnAppMoveToNullIsADeliberateClear_AndSaysSo()
    {
        // Elvanto ignores an explicit null and an omitted field alike, so a clear can only be
        // expressed as an empty string, by the descriptor, deliberately. Naming it in the reason is
        // what keeps "cleared" distinguishable from "had nothing to say" once the value is gone.
        FieldDecision d = Reconciler.Decide(
                With(Plain, SyncDirection.Bidirectional),
                Compare(Plain, null, "elvanto", baseApp: "elvanto", baseElvanto: "elvanto", hasBase: true));

        Assert.Equal(FieldDecisionKind.Outbound, d.Kind);
        Assert.Equal("AppClearedTheField", d.Reason);
    }

    // ---------------------------------------------------------------- rows 11, 12

    [Fact]
    public void Row11_NoBaseAppEditedElvantoUnusable_IsOutboundAndStaysOutstanding()
    {
        // Was: outbound was reported and a snapshot was created regardless, so the change was
        // invisible forever if the push did not land.
        FieldDecision d = Reconciler.Decide(
                With(SaysNothingIsUnusable, SyncDirection.Bidirectional),
                Compare(SaysNothingIsUnusable, "edited", "None"));

        Assert.Equal(FieldDecisionKind.Outbound, d.Kind);
        Assert.Equal("FirstSync:ElvantoHasNothing", d.Reason);
    }

    [Fact]
    public void Row12_NoBaseAppEditedElvantoUsable_AppliesFirstSyncPrecedence()
    {
        // Was: the app's edit took the outbound branch and FirstSyncPrecedence was bypassed
        // entirely. With no base neither side has a history, which is exactly what that hook is for.
        FieldDecision d = Reconciler.Decide(
                With(Plain, SyncDirection.Bidirectional),
                Compare(Plain, "edited", "elvanto"));

        Assert.Equal(FieldDecisionKind.Inbound, d.Kind);
        Assert.Equal("FirstSync:ElvantoPrecedence", d.Reason);
    }

    // ---------------------------------------------------------------- rows 13, 14

    [Fact]
    public void Rows13And14_FirstSyncWhereTheAppIsTheSystemOfRecord_MergesRatherThanOverwrites()
    {
        // Was: the merge was computed, reported, and then destroyed by the same run creating a
        // snapshot - one writes-off run was enough to lose the documented first-sync behaviour.
        FieldDecision d = Reconciler.Decide(
                With(AppWinsFirstSync, SyncDirection.Bidirectional),
                Compare(AppWinsFirstSync, "Allergies: Peanuts", "Eggs & Milk"));

        Assert.Equal(FieldDecisionKind.Outbound, d.Kind);
        Assert.Equal("FirstSync:AppPrecedence", d.Reason);
        Assert.Equal("Allergies: Peanuts\nEggs & Milk", d.Value);
    }

    [Fact]
    public void Rows13And14_AppPrecedenceNeedsTheAppToActuallyHaveSomething()
    {
        FieldDecision d = Reconciler.Decide(
                With(AppWinsFirstSync, SyncDirection.Bidirectional),
                Compare(AppWinsFirstSync, "   ", "Eggs & Milk"));

        Assert.Equal(FieldDecisionKind.Inbound, d.Kind);
        Assert.Equal("FirstSync:ElvantoPrecedence", d.Reason);
    }

    // ---------------------------------------------------------------- rows 15, 16

    [Fact]
    public void Row15_AppMovedAloneOnAnInboundOnlyField_IsARefusalWithAName()
    {
        // Was: silent. No branch matched, nothing logged, nothing counted.
        FieldDecision d = Reconciler.Decide(
                With(Plain, SyncDirection.InboundOnly),
                Compare(Plain, "new", "elvanto", baseApp: "elvanto", baseElvanto: "elvanto", hasBase: true));

        Assert.Equal(FieldDecisionKind.Diverged, d.Kind);
        Assert.Equal("DirectionRefused:InboundOnly:Outbound:AppChangedAlone", d.Reason);
    }

    [Fact]
    public void Row16_NoBaseAppEditedOnAnInboundOnlyField_AppliesElvantoInstead()
    {
        // Was: nothing happened AND a snapshot was created, so the app's change was consumed and
        // Elvanto's value was not applied either.
        FieldDecision d = Reconciler.Decide(
                With(Plain, SyncDirection.InboundOnly),
                Compare(Plain, "edited", "elvanto"));

        Assert.Equal(FieldDecisionKind.Inbound, d.Kind);
    }

    // ---------------------------------------------------------------- rows 17, 18

    [Fact]
    public void Row17_BothMoved_GoesToTheResolverAndElvantosNewerEditWins()
    {
        FieldDecision d = Reconciler.Decide(
                With(Plain, SyncDirection.Bidirectional),
                Compare(Plain, "appNew", "elvNew", baseApp: "old", baseElvanto: "old", hasBase: true,
                appChangedAt: new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero),
                elvantoChangedAt: new DateTimeOffset(2026, 8, 20, 0, 0, 0, TimeSpan.Zero)));

        Assert.Equal(FieldDecisionKind.Inbound, d.Kind);
        Assert.True(d.WasConflict);
        Assert.Equal("LastWriteWins:ElvantoNewer", d.Reason);
    }

    [Fact]
    public void Row17_BothMovedAndTheAppEditedLater_PushesTheApp()
    {
        FieldDecision d = Reconciler.Decide(
                With(Plain, SyncDirection.Bidirectional),
                Compare(Plain, "appNew", "elvNew", baseApp: "old", baseElvanto: "old", hasBase: true,
                appChangedAt: new DateTimeOffset(2026, 8, 20, 0, 0, 0, TimeSpan.Zero),
                elvantoChangedAt: new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero)));

        Assert.Equal(FieldDecisionKind.Outbound, d.Kind);
        Assert.True(d.WasConflict);
        Assert.Equal("LastWriteWins:AppNewer", d.Reason);
    }

    [Fact]
    public void Row17_AMissingAppTimestampNoLongerMeansTheAppDidNotChange()
    {
        // The change log is a tiebreak now, not an admission gate. With no row for the app the
        // conflict still happens, and falls through to whichever side actually has a value.
        FieldDecision d = Reconciler.Decide(
                With(Plain, SyncDirection.Bidirectional, PrecedenceOnTie.App),
                Compare(Plain, "appNew", "elvNew", baseApp: "old", baseElvanto: "old", hasBase: true,
                appChangedAt: null,
                elvantoChangedAt: new DateTimeOffset(2026, 8, 20, 0, 0, 0, TimeSpan.Zero)));

        Assert.True(d.WasConflict);
        Assert.Equal(FieldDecisionKind.Outbound, d.Kind);
        Assert.Equal("PrecedenceOnTie:App", d.Reason);
    }

    [Fact]
    public void Row18_BothMovedOnAnInboundOnlyField_StillAppliesElvantoWhenElvantoWins()
    {
        // Was: neither change was applied and the snapshot advanced anyway - on a field whose whole
        // declared point is that Elvanto wins.
        FieldDecision d = Reconciler.Decide(
                With(Plain, SyncDirection.InboundOnly),
                Compare(Plain, "appNew", "elvNew", baseApp: "old", baseElvanto: "old", hasBase: true,
                appChangedAt: new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero),
                elvantoChangedAt: new DateTimeOffset(2026, 8, 20, 0, 0, 0, TimeSpan.Zero)));

        Assert.Equal(FieldDecisionKind.Inbound, d.Kind);
        Assert.True(d.WasConflict);
    }

    [Fact]
    public void Row18_BothMovedOnAnInboundOnlyFieldAndTheAppWins_IsARefusalWithAName()
    {
        FieldDecision d = Reconciler.Decide(
                With(Plain, SyncDirection.InboundOnly),
                Compare(Plain, "appNew", "elvNew", baseApp: "old", baseElvanto: "old", hasBase: true,
                appChangedAt: new DateTimeOffset(2026, 8, 20, 0, 0, 0, TimeSpan.Zero),
                elvantoChangedAt: new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero)));

        Assert.Equal(FieldDecisionKind.Diverged, d.Kind);
        Assert.Contains("DirectionRefused:InboundOnly:Outbound:LastWriteWins:AppNewer", d.Reason);
    }

    // ---------------------------------------------------------------- row 19

    [Fact]
    public void Row19_BothMovedButElvantosValueSaysNothing_NeverLetsItWin()
    {
        // Was: outbound was reported and the snapshot advanced, consuming the app's change if the
        // push did not land.
        FieldDecision d = Reconciler.Decide(
                With(SaysNothingIsUnusable, SyncDirection.Bidirectional),
                Compare(SaysNothingIsUnusable, "appNew", "None", baseApp: "old", baseElvanto: "old", hasBase: true));

        Assert.Equal(FieldDecisionKind.Outbound, d.Kind);
        Assert.Equal("Conflict:ElvantoValueNotUsable", d.Reason);
        Assert.True(d.WasConflict);
    }

    // -------------------------------------------- textually different, substantively the same

    [Fact]
    public void NeitherSideSayingAnythingIsAgreement_NotADivergence()
    {
        // The app holds nothing and Elvanto's box says "None". The hashes differ, a person reading
        // the run would not call that a difference, and left unsettled it reported once per person
        // per run forever - 89 rows on the first real run, in exactly the place the divergences are
        // supposed to be a work-list.
        FieldDecision d = Reconciler.Decide(
                With(SaysNothingIsUnusable, SyncDirection.Bidirectional),
                Compare(SaysNothingIsUnusable, null, "None"));

        Assert.Equal(FieldDecisionKind.Agreed, d.Kind);
        Assert.Equal("Match:NeitherSideSaysAnything", d.Reason);
    }

    [Fact]
    public void AppHavingSomethingIsStillOutbound_EvenWhenElvantoSaysNothing()
    {
        // The other half of the same rule, and the one that must not be swallowed by it: this is the
        // restored-dump backlog.
        Assert.Equal(
            FieldDecisionKind.Outbound,
            Reconciler.Decide(
                With(SaysNothingIsUnusable, SyncDirection.Bidirectional),
                Compare(SaysNothingIsUnusable, "0435862120", "None")).Kind);
    }

    // ------------------------------------------------- an unknown side is not a value

    [Fact]
    public void AnUnknownElvantoSideIsReportedToo_NotAppliedAsAClearedFamily()
    {
        // The mirror. A blank family_id means Elvanto has no household for this person; applying it
        // inbound runs SetOnApp with null, which mints a fresh Guid and puts them in a brand-new
        // one-person household — 411 people on a real run.
        FieldComparison c = new()
        {
            AppValue           = "4901",
            ElvantoValue       = null,
            AppHash            = Plain.Hash("4901"),
            ElvantoHash        = Plain.Hash(null),
            BaseAppHash        = null,
            BaseElvantoHash    = null,
            ElvantoValueUsable = true,
            ElvantoValueKnown  = false,
            AppChangedAt       = null,
            ElvantoChangedAt   = null
        };

        FieldDecision d = Reconciler.Decide(
                With(Plain, SyncDirection.Bidirectional),
                c);

        Assert.Equal(FieldDecisionKind.Diverged, d.Kind);
        Assert.Equal("ElvantoValueUnknown", d.Reason);
    }

    [Fact]
    public void AKnownEmptyAppSideIsStillADeliberateClear()
    {
        // The other half: when the app genuinely holds nothing and we know it, that is a clear and
        // must still be pushed. Unknown and empty are different answers.
        FieldDecision d = Reconciler.Decide(
                With(Plain, SyncDirection.Bidirectional),
                Compare(Plain, null, "4615", baseApp: "4615", baseElvanto: "4615", hasBase: true));

        Assert.Equal(FieldDecisionKind.Outbound, d.Kind);
        Assert.Equal("AppClearedTheField", d.Reason);
    }

    // ------------------------------------------------------- the migration's own case

    [Fact]
    public void ARowWrittenBeforeTheAppLegExisted_ReadsAsNoBase()
    {
        // AppHash is deliberately not backfilled. A null app leg means "no base", so the first run
        // after the migration re-applies first-sync rules and surfaces every divergence that was
        // invisible - which is the migration's most valuable side effect, not a gap in it.
        FieldComparison c = new()
        {
            AppValue           = "app",
            ElvantoValue       = "elvanto",
            AppHash            = Plain.Hash("app"),
            ElvantoHash        = Plain.Hash("elvanto"),
            BaseAppHash        = null,
            BaseElvantoHash    = Plain.Hash("elvanto"),
            ElvantoValueUsable = true,
            AppChangedAt       = null,
            ElvantoChangedAt   = null
        };

        Assert.False(c.HasBase);
        Assert.Equal(FieldDecisionKind.Inbound,
            Reconciler.Decide(
                With(Plain, SyncDirection.Bidirectional),
                c).Kind);
    }

    // ------------------------------------------------- no path may end in nothing

    [Theory]
    [InlineData(SyncDirection.Bidirectional)]
    [InlineData(SyncDirection.InboundOnly)]
    [InlineData(SyncDirection.OutboundOnly)]
    public void EveryCombinationOfMovementAndUsabilityProducesADecision(SyncDirection direction)
    {
        // The point of the whole exercise: silence is not an outcome. Every reachable state
        // produces a decision, and each one is either an action or a named refusal.
        foreach (bool hasBase in new[] { true, false })
        foreach (bool appMoved in new[] { true, false })
        foreach (bool elvMoved in new[] { true, false })
        foreach (bool usable in new[] { true, false })
        {
            TruthTableDescriptor desc = usable ? Plain : SaysNothingIsUnusable;
            string? elvValue = usable ? "elvanto" : "None";

            FieldDecision d = Reconciler.Decide(
                With(desc, direction),
                Compare(desc,
                    appValue: appMoved ? "appMoved" : "appBase",
                    elvValue: elvValue,
                    baseApp: "appBase",
                    baseElvanto: elvMoved ? "elvBase" : elvValue,
                    hasBase: hasBase));

            Assert.True(
                d.Kind is FieldDecisionKind.Agreed or FieldDecisionKind.Inbound
                    or FieldDecisionKind.Outbound or FieldDecisionKind.Diverged,
                $"base={hasBase} app={appMoved} elv={elvMoved} usable={usable} dir={direction} -> {d.Kind}");
            Assert.False(string.IsNullOrWhiteSpace(d.Reason));
        }
    }
}
