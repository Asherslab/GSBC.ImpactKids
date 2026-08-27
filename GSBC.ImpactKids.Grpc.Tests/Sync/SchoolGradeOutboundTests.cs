using System.Text.Json;
using GSBC.ImpactKids.Grpc.Data.Models.Sync.Enums;
using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Models;
using GSBC.ImpactKids.Grpc.Features.People.Sync.Descriptors;
using GSBC.ImpactKids.Grpc.Features.People.Sync.Interfaces;
using GSBC.ImpactKids.Grpc.Features.People.Sync.Models;
using GSBC.ImpactKids.Grpc.Features.People.Sync.Services;

namespace GSBC.ImpactKids.Grpc.Tests.Sync;

/// <summary>
/// School grade goes both ways now, and the two things that kept it InboundOnly have to stay fixed:
/// the payload has to genuinely carry the field, and it has to carry Elvanto's id rather than the
/// local Guid the comparison speaks in.
///
/// The pairing matters more than either half. A Bidirectional direction over a payload that drops
/// the field is the exact combination that wrote "would push" audit rows for a change that could
/// never reach Elvanto.
/// </summary>
public class SchoolGradeOutboundTests
{
    private static readonly SchoolGradeDescriptor Grade      = new();
    private static readonly IFieldReconciler      Reconciler = new FieldReconciler(new ConflictResolver());

    // Elvanto's ids, as they arrive under school_grade.id and are stored on DbSchoolGrade.ElvantoId.
    private const string Year3 = "8f1c0a3e-year3";
    private const string Year4 = "8f1c0a3e-year4";

    [Fact]
    public void TheDirectionIsBidirectional()
    {
        Assert.Equal(SyncDirection.Bidirectional, Grade.DefaultDirection);
    }

    [Fact]
    public void AGradeIdIsCarriedOnThePayload()
    {
        ElvantoUpdatePersonRequest req = new() { Id = "person-1" };

        Assert.True(Grade.ApplyToElvantoRequest(req, Year3));
        Assert.Equal(Year3, req.SchoolGrade);
    }

    /// <summary>
    /// Where it sits on the wire, which is the half that was wrong. <c>school_grade</c> is a standard
    /// optional people field: under <c>fields</c> it is accepted, at the top level it is rejected
    /// outright with "A param does not exist (school_grade)", verified against the live API on
    /// 2026-08-27.
    /// </summary>
    [Fact]
    public void TheGradeTravelsUnderFieldsAndNotAtTheTopLevel()
    {
        ElvantoUpdatePersonRequest req = new() { Id = "person-1" };
        Grade.ApplyToElvantoRequest(req, Year3);

        string json = JsonSerializer.Serialize(req);

        using JsonDocument doc = JsonDocument.Parse(json);
        Assert.False(doc.RootElement.TryGetProperty("school_grade", out _));
        Assert.Equal(Year3, doc.RootElement.GetProperty("fields").GetProperty("school_grade").GetString());
    }

    /// <summary>
    /// A person with no grade to push carries no <c>fields</c> object at all, rather than one holding
    /// a null or an empty string. Elvanto answers a 500 to an empty <c>school_grade</c> and silently
    /// ignores a null, so neither is a clear and neither should ever be built.
    /// </summary>
    [Fact]
    public void DecliningTheGradeLeavesNothingOnTheWire()
    {
        ElvantoUpdatePersonRequest req = new() { Id = "person-1" };
        Grade.ApplyToElvantoRequest(req, null);

        using JsonDocument doc = JsonDocument.Parse(JsonSerializer.Serialize(req));
        Assert.False(doc.RootElement.TryGetProperty("fields", out _));
    }

    /// <summary>
    /// Null reaches here for a child with no grade and for a local grade row with no
    /// <c>ElvantoId</c>. Neither is an instruction to empty a grade Elvanto maintains, and a
    /// declined field is reported as <c>NotCarried:</c> rather than pushed.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void NothingToNameInElvantosTermsIsDeclinedRatherThanSentAsAClear(string? value)
    {
        ElvantoUpdatePersonRequest req = new() { Id = "person-1" };

        Assert.False(Grade.ApplyToElvantoRequest(req, value));
        Assert.Null(req.SchoolGrade);
    }

    /// <summary>
    /// The yearly rollover, which is the whole reason this field was InboundOnly. Elvanto's leg moved
    /// and the app's did not, so it is applied inbound - no clock consulted, and nothing pushed back.
    /// </summary>
    [Fact]
    public void TheYearlyRolloverStillWinsInbound()
    {
        FieldDecision d = Reconciler.Decide(Grade, Compare(
            appValue: Year3, elvValue: Year4, baseApp: Year3, baseElvanto: Year3));

        Assert.Equal(FieldDecisionKind.Inbound, d.Kind);
        Assert.Equal("ElvantoChangedAlone", d.Reason);
    }

    /// <summary>
    /// The row that used to read <c>DirectionRefused:InboundOnly:Outbound:AppChangedAlone</c>. A
    /// leader correcting a grade in the app is now a push rather than a permanent divergence.
    /// </summary>
    [Fact]
    public void AGradeCorrectedInTheAppNowPushes()
    {
        FieldDecision d = Reconciler.Decide(Grade, Compare(
            appValue: Year4, elvValue: Year3, baseApp: Year3, baseElvanto: Year3));

        Assert.Equal(FieldDecisionKind.Outbound, d.Kind);
        Assert.Equal("AppChangedAlone", d.Reason);
    }

    /// <summary>
    /// First sync with a grade Elvanto has never been told about. This was
    /// <c>DirectionRefused:InboundOnly:Outbound:FirstSync:ElvantoHasNothing</c> - the shape of the
    /// whole restored-dump backlog.
    /// </summary>
    [Fact]
    public void AFirstSyncGradeElvantoDoesNotHaveIsPushed()
    {
        FieldDecision d = Reconciler.Decide(Grade, Compare(appValue: Year3, elvValue: null));

        Assert.Equal(FieldDecisionKind.Outbound, d.Kind);
        Assert.Equal("FirstSync:ElvantoHasNothing", d.Reason);
    }

    /// <summary>
    /// Elvanto still wins a first sync where it has a grade, and still wins a genuine tie: the
    /// descriptor's <see cref="SchoolGradeDescriptor.PrecedenceOnTie"/> is unchanged.
    /// </summary>
    [Fact]
    public void ElvantoStillWinsAFirstSyncItCanAnswer()
    {
        FieldDecision d = Reconciler.Decide(Grade, Compare(appValue: Year3, elvValue: Year4));

        Assert.Equal(FieldDecisionKind.Inbound, d.Kind);
        Assert.Equal("FirstSync:ElvantoPrecedence", d.Reason);
    }

    /// <summary>
    /// Values are in the comparison space the orchestrator normalises to - the app's - which is why
    /// both legs here are the same kind of string. The outbound translation back to Elvanto's id
    /// happens in <c>BuildComparison</c>, above the reconciler.
    /// </summary>
    private static FieldComparison Compare(
        string? appValue,
        string? elvValue,
        string? baseApp     = null,
        string? baseElvanto = null) => new()
    {
        AppValue           = appValue,
        ElvantoValue       = elvValue,
        AppHash            = Grade.Hash(appValue),
        ElvantoHash        = Grade.Hash(elvValue),
        BaseAppHash        = baseApp is null ? null : Grade.Hash(baseApp),
        BaseElvantoHash    = baseElvanto is null ? null : Grade.Hash(baseElvanto),
        ElvantoValueUsable = Grade.IsValidInboundValue(elvValue),
        AppChangedAt       = null,
        ElvantoChangedAt   = null
    };
}
