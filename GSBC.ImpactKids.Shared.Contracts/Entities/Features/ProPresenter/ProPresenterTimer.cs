namespace GSBC.ImpactKids.Shared.Contracts.Entities.Features.ProPresenter;

/// <summary>One of the timers set up in ProPresenter, as the agent last saw them. The uuid is what is stored; the name is for people.</summary>
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public record ProPresenterTimer
{
    public required string Uuid { get; init; }
    public required string Name { get; init; }
}
