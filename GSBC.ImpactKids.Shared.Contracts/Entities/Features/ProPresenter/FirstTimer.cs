namespace GSBC.ImpactKids.Shared.Contracts.Entities.Features.ProPresenter;

/// <summary>
/// A child at Impact Kids for the first time today, shaped for a screen in the room.
/// <para>
/// <b>A first name and nothing else, on purpose.</b> This goes to a ProPresenter output that the
/// whole room can see, and to a machine with no person at it. Do not add a surname, a photo, an age
/// or anything from the medical record - a welcome needs none of them.
/// </para>
/// </summary>
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public record FirstTimer
{
    public required Guid   PersonId  { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
}
