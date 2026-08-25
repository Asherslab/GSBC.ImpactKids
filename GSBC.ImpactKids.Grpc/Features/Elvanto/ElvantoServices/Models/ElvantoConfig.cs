using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Elvanto;

namespace GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Models;

public class ElvantoConfig
{
    public required string                  Authentication { get; set; }
    public          ElvantoReportResponse[] Reports        { get; set; } = [];

    /// <summary>
    /// Master switch for every write to Elvanto (people/create and people/edit).
    /// Defaults to <c>false</c>, so an environment that says nothing about it cannot push.
    /// Turning it on is the only way a mutation leaves this process - see
    /// <c>ElvantoService.SendMessage</c>, which refuses to hand a mutation to HttpClient
    /// while this is false, whatever the calling code does.
    /// Reads (people/getAll, people/getInfo, services/getAll) are unaffected.
    /// </summary>
    public bool AllowWrites { get; set; }

    /// <summary>
    /// Per-endpoint switches beneath <see cref="AllowWrites"/>. Both default to <c>false</c>, so
    /// turning on <see cref="AllowWrites"/> alone still sends nothing - deliberate, because the
    /// first real write should have to name what kind of write it is. Steady-state operation sets
    /// both to <c>true</c>.
    /// </summary>
    public bool AllowCreates { get; set; }

    /// <inheritdoc cref="AllowCreates"/>
    public bool AllowUpdates { get; set; }

    /// <summary>
    /// Hard ceiling on mutations for the lifetime of the process. Null means no ceiling.
    /// See <see cref="ElvantoWriteBudget"/>.
    /// </summary>
    public int? MaxWrites { get; set; }

    /// <summary>
    /// App person ids that may be created in Elvanto. Empty means no restriction. When set, every
    /// other create is suppressed and audited, so a run can be pointed at one person without
    /// relying on scope - which for an unlinked person pulls the whole Elvanto roll anyway.
    /// </summary>
    public Guid[] AllowedCreatePersonIds { get; set; } = [];

    /// <summary>
    /// App person ids whose field changes may be pushed to Elvanto. Empty means no restriction.
    /// The counterpart to <see cref="AllowedCreatePersonIds"/>: without it, a controlled update test
    /// also ships every unrelated change that happens to be pending, which is how an unnoticed edit
    /// from weeks ago reaches Elvanto on the back of someone else's test.
    /// </summary>
    public Guid[] AllowedUpdatePersonIds { get; set; } = [];
}