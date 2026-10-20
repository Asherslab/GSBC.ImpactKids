using System.Collections.Immutable;

namespace GSBC.ImpactKids.Shared.Contracts.Entities.Features.Songs;

/// <summary>
/// A song from ProPresenter, for song leaders to read and choose from. Read only here: ProPresenter
/// is where songs are written, and the agent on its Mac copies them in - see
/// <c>docs/work/2026-10-songs-library.md</c>.
/// <para>
/// The lyrics travel with the list. A song is a few kilobytes of text and the library is tens of
/// songs, so the page searches lyrics on the phone rather than asking the server per song.
/// </para>
/// </summary>
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public record Song : IIdentifiable
{
    public required Guid   Id   { get; init; }
    public required string Name { get; init; }

    /// <summary>The ProPresenter library it is in, by name - "Songs".</summary>
    public required string Library { get; init; }

    /// <summary>The song's groups in ProPresenter's order - each verse or chorus once.</summary>
    public ImmutableList<SongGroup> Groups { get; init; } = [];

    /// <summary>The named orders it is sung in. ProPresenter's own "Master" - every group once - is not one of them.</summary>
    public ImmutableList<SongArrangement> Arrangements { get; init; } = [];

    /// <summary>The arrangement ProPresenter plays by default. Null means every group in order.</summary>
    public string? DefaultArrangementUuid { get; init; }

    /// <summary>
    /// When Impact Kids first saw the current words, groups and arrangements - a sync that finds the
    /// song unchanged leaves it alone. Not when it was edited in ProPresenter, which the API does not say.
    /// </summary>
    public required DateTime UpdatedAt { get; init; }

    /// <summary>
    /// The date of the last service, up to today, whose schedule has this song - in UTC, as the
    /// service's date is. Null if it has never been put in one. Only songs chosen from the library
    /// count; one typed by name before it was in ProPresenter does not.
    /// </summary>
    public DateTime? LastSungAt { get; init; }

    /// <summary>How many services, up to today, had it in their schedule. Twice in one night counts once.</summary>
    public int TimesSung { get; init; }

    /// <summary>The groups as sung in <paramref name="arrangementUuid"/>, repeats included; every group once for null or an unknown arrangement.</summary>
    public IEnumerable<SongGroup> Sung(string? arrangementUuid)
    {
        SongArrangement? arrangement = Arrangements.FirstOrDefault(x => x.Uuid == arrangementUuid);

        if (arrangement == null)
            return Groups;

        return arrangement.GroupUuids
            .Select(uuid => Groups.FirstOrDefault(g => g.Uuid == uuid))
            .OfType<SongGroup>();
    }
}

/// <summary>A verse, chorus or bridge: ProPresenter's group, with the text of each of its slides.</summary>
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public record SongGroup
{
    public required string Uuid { get; init; }
    public required string Name { get; init; }

    /// <summary>The group's colour in ProPresenter, as <c>#RRGGBB</c>. Null when it has none.</summary>
    public string? Color { get; init; }

    /// <summary>Each slide's words, line breaks kept. Blank slides are left out.</summary>
    public ImmutableList<string> Slides { get; init; } = [];
}

/// <summary>An order to sing the groups in - "Default", or one made for a night like "21/11/25".</summary>
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public record SongArrangement
{
    public required string Uuid { get; init; }
    public required string Name { get; init; }

    /// <summary>The groups in singing order. A chorus sung three times is here three times.</summary>
    public ImmutableList<string> GroupUuids { get; init; } = [];
}
