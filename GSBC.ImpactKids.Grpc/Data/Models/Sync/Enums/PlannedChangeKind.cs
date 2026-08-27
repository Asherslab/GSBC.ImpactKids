namespace GSBC.ImpactKids.Grpc.Data.Models.Sync.Enums;

/// <summary>
/// The six kinds of work a sync can decide on. Apply executes exactly these and discovers nothing:
/// anything that appeared since Decide belongs to the next plan, and saying so plainly is what makes
/// the button safe to press.
/// </summary>
public enum PlannedChangeKind
{
    /// <summary>Write one field onto the app person from Elvanto.</summary>
    InboundField,

    /// <summary>Push one field to Elvanto.</summary>
    OutboundField,

    /// <summary>Create the app person in Elvanto.</summary>
    CreateInElvanto,

    /// <summary>Create a local person from an Elvanto record that matched nobody.</summary>
    CreateLocally,

    /// <summary>Soft-delete an app person whose Elvanto record is gone.</summary>
    Archive,

    /// <summary>Set <c>DbPerson.ElvantoId</c>, linking an existing app person to an Elvanto record.</summary>
    LinkPerson
}

public enum PlannedChangeStatus
{
    /// <summary>Decided, not yet applied. The only status Apply will act on.</summary>
    Pending,

    Applied,

    /// <summary>Deliberately not applied — a write guard, an allow list, or a budget.</summary>
    Skipped,

    /// <summary>
    /// One of the two sides moved between Decide and Apply. Skipped and reported rather than
    /// clobbered on the strength of a stale reading, which is the guarantee the plan exists to give.
    /// </summary>
    Stale,

    Failed
}
