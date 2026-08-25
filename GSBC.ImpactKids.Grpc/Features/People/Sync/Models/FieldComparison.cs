using GSBC.ImpactKids.Grpc.Data.Models.Sync.Enums;

namespace GSBC.ImpactKids.Grpc.Features.People.Sync.Models;

/// <summary>
/// One field on one person, with both sides and the base, ready to be decided.
///
/// <b>Every value here is in the same comparison space.</b> Two fields are not naturally: family is
/// compared in Elvanto's terms (a numeric family id) because a local Guid translated back through
/// the family map compares equal to itself, and school grade is compared in the app's. The
/// orchestrator normalises both before building this, and carries the value each side actually
/// wants written in <see cref="InboundValue"/> and <see cref="OutboundValue"/>.
/// </summary>
public sealed record FieldComparison
{
    /// <summary>The app's value, in the comparison space. What the base's app leg stores.</summary>
    public required string? AppValue { get; init; }

    /// <summary>Elvanto's value, in the comparison space. What the base's Elvanto leg stores.</summary>
    public required string? ElvantoValue { get; init; }

    public required string AppHash     { get; init; }
    public required string ElvantoHash { get; init; }

    /// <summary>
    /// The base's app leg. <b>Null means there is no base</b> and first-sync rules apply — including
    /// for every row written before the column existed, which is not a gap but the point.
    /// </summary>
    public required string? BaseAppHash { get; init; }

    /// <summary>The base's Elvanto leg. Only meaningful when <see cref="BaseAppHash"/> is set.</summary>
    public required string? BaseElvantoHash { get; init; }

    /// <summary>
    /// False when Elvanto's value is semantically empty for this field ("Not Requested" consent,
    /// "None" in the medical box). Such a value may never drive an inbound write or win a conflict.
    /// </summary>
    public required bool ElvantoValueUsable { get; init; }

    /// <summary>
    /// The app's last edit to this field, from <c>FieldChangeLogs</c>. Consulted <b>only</b> to break a
    /// genuine two-sided conflict. A missing row now means "app timestamp unknown", not "the app did
    /// not change" — that inference is what made a real difference invisible.
    /// </summary>
    public required DateTimeOffset? AppChangedAt { get; init; }

    /// <summary>Elvanto's own <c>date_modified</c>. Per person, so an upper bound, which only a conflict tolerates.</summary>
    public required DateTimeOffset? ElvantoChangedAt { get; init; }

    /// <summary>What <c>SetOnApp</c> should receive when Elvanto wins. Defaults to <see cref="ElvantoValue"/>.</summary>
    public string? InboundValue { get; init; }

    /// <summary>What should be pushed when the app wins. Defaults to <see cref="AppValue"/>.</summary>
    public string? OutboundValue { get; init; }

    public string? EffectiveInboundValue  => InboundValue  ?? ElvantoValue;
    public string? EffectiveOutboundValue => OutboundValue ?? AppValue;

    public bool HasBase => BaseAppHash is not null;
}

public enum FieldDecisionKind
{
    /// <summary>Direction is Disabled. Nothing happens and the base is not touched.</summary>
    Skipped,

    /// <summary>The two sides already say the same thing. The base is written to say so.</summary>
    Agreed,

    /// <summary>Elvanto's value is applied to the app, and the base advances with it.</summary>
    Inbound,

    /// <summary>
    /// The app's value is pushed. <b>The base advances only if the request that was actually built
    /// carried this field</b> — not if the descriptor was asked, and not if the call returned ok.
    /// </summary>
    Outbound,

    /// <summary>The two sides differ and nothing may be done about it. Audited; the base is left alone.</summary>
    Diverged
}

/// <summary>
/// What to do about one field, and why. <see cref="Reason"/> is written verbatim into the audit row,
/// so it has to read as an explanation on its own.
/// </summary>
public sealed record FieldDecision(
    FieldDecisionKind Kind,
    string?           Value,
    string            Reason,
    bool              WasConflict = false)
{
    public static FieldDecision Skipped(string reason)  => new(FieldDecisionKind.Skipped,  null, reason);
    public static FieldDecision Agreed(string reason = "Match") => new(FieldDecisionKind.Agreed, null, reason);
    public static FieldDecision Diverged(string reason) => new(FieldDecisionKind.Diverged, null, reason);

    public static FieldDecision Inbound(string? value, string reason, bool conflict = false) =>
        new(FieldDecisionKind.Inbound, value, reason, conflict);

    public static FieldDecision Outbound(string? value, string reason, bool conflict = false) =>
        new(FieldDecisionKind.Outbound, value, reason, conflict);

    public SyncSource? WinningSide => Kind switch
    {
        FieldDecisionKind.Inbound  => SyncSource.Elvanto,
        FieldDecisionKind.Outbound => SyncSource.App,
        _                          => null
    };
}
