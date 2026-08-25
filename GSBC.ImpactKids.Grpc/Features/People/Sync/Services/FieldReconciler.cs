using GSBC.ImpactKids.Grpc.Data.Models.Sync;
using GSBC.ImpactKids.Grpc.Data.Models.Sync.Enums;
using GSBC.ImpactKids.Grpc.Features.People.Sync.Interfaces;
using GSBC.ImpactKids.Grpc.Features.People.Sync.Models;

namespace GSBC.ImpactKids.Grpc.Features.People.Sync.Services;

/// <summary>
/// The three-way merge. Given what each side holds now and what both held the last time they
/// agreed, decides which side moved — without consulting a clock.
///
/// The engine used to answer "did the app change?" by comparing an edit timestamp against a
/// <i>poll</i> timestamp. Those are two independent clocks, and every way they drifted made a real,
/// visible difference permanently invisible: a restored dump has no change-log rows at all, so
/// every app value Elvanto lacked was discarded on every run, forever, reported as "nothing to push".
///
/// Two rules give this its shape:
///
/// <list type="number">
/// <item><b>Decide first, then let direction filter.</b> Direction used to be part of change
/// detection, so a field the direction refused was never compared at all — and on a field declared
/// InboundOnly with both sides moved, Elvanto's change was dropped too, on a field whose whole point
/// is that Elvanto wins. The comparison is now unconditional and direction refuses an
/// <i>outcome</i>, which is a refusal that can be named in an audit row.</item>
/// <item><b>Timestamps only break a genuine two-sided conflict.</b> Elvanto's <c>date_modified</c>
/// is per person, not per field, so it is an upper bound — acceptable as a tiebreak, ruinous as a
/// gate. A missing app-side row now means "unknown", not "the app did not change".</item>
/// </list>
/// </summary>
public class FieldReconciler(IConflictResolver conflictResolver) : IFieldReconciler
{
    public FieldDecision Decide(
        IFieldSyncDescriptor descriptor,
        FieldComparison      comparison,
        DbSyncFieldConfig    config)
    {
        if (config.Direction == SyncDirection.Disabled)
            return FieldDecision.Skipped("Direction:Disabled");

        if (comparison.AppHash == comparison.ElvantoHash)
            return FieldDecision.Agreed();

        FieldDecision decision = comparison.HasBase
            ? DecideAgainstBase(descriptor, comparison, config)
            : DecideFirstSync(descriptor, comparison);

        return ApplyDirection(decision, comparison, config);
    }

    /// <summary>
    /// No base, so neither side has a trustworthy history. Most fields let Elvanto win; the
    /// medical/allergy box is the app's to state, because the app holds structured records a leader
    /// entered rather than whatever text happened to be in the field — and it merges rather than
    /// overwrites, so Elvanto text the app does not already say survives.
    /// </summary>
    private static FieldDecision DecideFirstSync(IFieldSyncDescriptor descriptor, FieldComparison c)
    {
        bool appHasSomethingToSay = !string.IsNullOrWhiteSpace(c.AppValue);

        if (descriptor.FirstSyncPrecedence == SyncSource.App && appHasSomethingToSay)
            return FieldDecision.Outbound(
                descriptor.MergeForFirstSync(c.EffectiveOutboundValue, c.ElvantoValue),
                "FirstSync:AppPrecedence");

        if (c.ElvantoValueUsable)
            return FieldDecision.Inbound(c.EffectiveInboundValue, "FirstSync:ElvantoPrecedence");

        // Elvanto's value says nothing ("Not Requested", "None", or simply absent) and the app has
        // something. This is the shape of the entire restored-dump backlog: an app value Elvanto has
        // never been told about. It was falling through to a bare continue with no row.
        if (appHasSomethingToSay)
            return FieldDecision.Outbound(c.EffectiveOutboundValue, "FirstSync:ElvantoHasNothing");

        return FieldDecision.Diverged("FirstSync:NeitherSideHasAValue");
    }

    private FieldDecision DecideAgainstBase(
        IFieldSyncDescriptor descriptor,
        FieldComparison      c,
        DbSyncFieldConfig    config)
    {
        bool appMoved = c.AppHash     != c.BaseAppHash;
        bool elvMoved = c.ElvantoHash != c.BaseElvantoHash;

        // The two sides differ and the base says neither has moved since they agreed - so the base
        // records a disagreement, which it can only do if it was written while one was outstanding.
        // Nothing can be inferred from it, so nothing is done to either side.
        if (!appMoved && !elvMoved)
            return FieldDecision.Diverged("BaseDisagreesWithBothSides");

        if (!appMoved)
            return FieldDecision.Inbound(c.EffectiveInboundValue, "ElvantoChangedAlone");

        if (!elvMoved)
            return FieldDecision.Outbound(c.EffectiveOutboundValue, DescribeAppChange(c));

        // Both moved. Elvanto's value can only win if it says something.
        if (!c.ElvantoValueUsable)
            return FieldDecision.Outbound(c.EffectiveOutboundValue, "Conflict:ElvantoValueNotUsable", conflict: true);

        ConflictResolution resolution = conflictResolver.Resolve(
            descriptor.FieldName, c.AppValue, c.AppChangedAt,
            c.ElvantoValue, c.ElvantoChangedAt, config);

        return resolution.WinningSide == SyncSource.Elvanto
            ? FieldDecision.Inbound(c.EffectiveInboundValue, resolution.Reason, conflict: true)
            : FieldDecision.Outbound(c.EffectiveOutboundValue, resolution.Reason, conflict: true);
    }

    /// <summary>
    /// An app-side move to null is a deliberate clear, and Elvanto only clears on an empty string:
    /// it answers <c>ok</c> to an explicit null and to an omitted field alike, and changes nothing.
    /// Saying so in the reason keeps the distinction visible in the audit trail, because "cleared"
    /// and "had nothing to say" look identical once the value is gone.
    /// </summary>
    private static string DescribeAppChange(FieldComparison c) =>
        string.IsNullOrEmpty(c.EffectiveOutboundValue) ? "AppClearedTheField" : "AppChangedAlone";

    /// <summary>
    /// The configured direction refuses an outcome, rather than pre-empting the comparison. A
    /// refusal is a finding with both values attached, not a field that was never looked at.
    /// </summary>
    private static FieldDecision ApplyDirection(
        FieldDecision     decision,
        FieldComparison   c,
        DbSyncFieldConfig config) => decision.Kind switch
    {
        FieldDecisionKind.Inbound when !c.ElvantoValueUsable =>
            FieldDecision.Diverged($"InvalidInboundValue:{decision.Reason}"),

        FieldDecisionKind.Inbound when config.Direction is not (SyncDirection.Bidirectional or SyncDirection.InboundOnly) =>
            FieldDecision.Diverged($"DirectionRefused:{config.Direction}:Inbound:{decision.Reason}"),

        FieldDecisionKind.Outbound when config.Direction is not (SyncDirection.Bidirectional or SyncDirection.OutboundOnly) =>
            FieldDecision.Diverged($"DirectionRefused:{config.Direction}:Outbound:{decision.Reason}"),

        _ => decision
    };
}
