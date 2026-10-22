namespace GSBC.ImpactKids.Shared.Contracts.Entities.Features.ProPresenter;

/// <summary>
/// One of ProPresenter's workspaces on the Mac, as the agent last saw them. The uuid is ProPresenter's
/// local id for the workspace on that Mac, and is what is stored; the name is for people.
/// <para>
/// Libraries cannot stand in for this: ProPresenter derives a library's uuid from its name, so the
/// "Songs" library of one workspace has the same uuid as the "Songs" library of another.
/// </para>
/// </summary>
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public record ProPresenterWorkspace
{
    public required string Uuid { get; init; }
    public required string Name { get; init; }
}
