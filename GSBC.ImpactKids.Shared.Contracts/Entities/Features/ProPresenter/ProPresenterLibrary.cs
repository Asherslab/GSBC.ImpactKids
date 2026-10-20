using System.Collections.Immutable;

namespace GSBC.ImpactKids.Shared.Contracts.Entities.Features.ProPresenter;

/// <summary>One of ProPresenter's libraries, as the agent last saw them. The uuid is what is stored; the name is for people.</summary>
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public record ProPresenterLibrary
{
    /// <summary>The library a machine copies songs from until a leader chooses: the one ProPresenter calls "Songs".</summary>
    public const string DefaultSongLibraryName = "Songs";

    public required string Uuid { get; init; }
    public required string Name { get; init; }

    /// <summary>
    /// The libraries a machine copies songs from. <paramref name="chosen"/> null means no leader has
    /// chosen yet, which is the reported library named <see cref="DefaultSongLibraryName"/>, if there
    /// is one; an empty choice is a choice, and copies nothing.
    /// <para>
    /// Here rather than on one side because the admin page and the agent's configuration must agree:
    /// the page shows the libraries ticked that the Mac is actually copying.
    /// </para>
    /// </summary>
    public static ImmutableList<string> SongLibraries(IEnumerable<string>? chosen, IEnumerable<ProPresenterLibrary> reported) =>
        chosen?.ToImmutableList()
        ?? reported.Where(x => x.Name == DefaultSongLibraryName).Select(x => x.Uuid).ToImmutableList();
}
