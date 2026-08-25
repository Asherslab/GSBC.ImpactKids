namespace GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices;

/// <summary>
/// Thrown when a people fetch could not be completed in full.
///
/// This exists because the alternative is worse than an error: the paging loop used to give up
/// on a failed page and return whatever it had collected, and the caller could not tell a
/// truncated list from a complete one. The sync then treated every linked person missing from
/// that partial list as deleted from Elvanto and archived them - 726 children in one dry run,
/// off the back of a single failed page.
///
/// A partial answer must never be mistaken for an authoritative one.
/// </summary>
public class ElvantoFetchException(string message) : Exception(message);
