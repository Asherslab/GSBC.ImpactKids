namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.People.Sync;

/// <summary>
/// Asks for a run to be decided. Carries nothing, and that is the shape of the decision rather than
/// an omission.
///
/// There used to be a Mode (Full / AppOnly / DryRun) and a Scope (All / Person / Family). Both are
/// gone. Every run now decides a plan and stops; making it happen is a separate, deliberate Execute
/// of a plan a person has read - so "which mode?" no longer has an answer to give. AppOnly is not
/// lost with it: an Execute with <c>Elvanto:AllowWrites=false</c> applies the inbound half and
/// records each outbound as suppressed, which is what AppOnly did, decided by configuration rather
/// than by whoever filled in a dropdown.
/// </summary>
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class SyncWithElvantoRequest;
