using System.Collections.Immutable;
using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.Shared.Contracts;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Songs;

namespace GSBC.ImpactKids.WASM.Features.Songs.Pages;

/// <summary>
/// The song library, for song leaders choosing what to sing. Searches the words as well as the name,
/// on the phone - the whole library is a few hundred kilobytes and is already in the store - because a
/// leader often remembers a line, not a title.
/// </summary>
public partial class Songs
{
    private AsyncData<ImmutableList<Song>> _songs = AsyncData<ImmutableList<Song>>.NotAsked();

    private string?                     _search;
    private ImmutableList<string>       _libraries       = [];
    private IReadOnlyCollection<string> _chosenLibraries = [];

    private ImmutableList<Song>     _shown    = [];
    private Dictionary<Guid, string> _previews = [];

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        HandleSubscriptionDisposal(SongsStore, RetrieveSongs);
        RetrieveSongs();

        await SongsStore.RefreshAll();
    }

    private void RetrieveSongs()
    {
        _songs = SongsStore.GetState().Entities;

        ImmutableList<string> libraries = _songs.HasData
            ? _songs.Data!.Select(x => x.Library).Distinct().Order().ToImmutableList()
            : [];

        // A library appearing for the first time starts ticked, like the rest; one a leader unticked stays unticked.
        if (!libraries.SequenceEqual(_libraries))
        {
            _chosenLibraries = libraries
                .Where(x => !_libraries.Contains(x) || _chosenLibraries.Contains(x))
                .ToList();
            _libraries = libraries;
        }

        Filter();
    }

    private void OnSearch(string? search)
    {
        _search = search;
        Filter();
    }

    private void OnLibrariesChanged(IReadOnlyCollection<string>? libraries)
    {
        _chosenLibraries = libraries ?? [];
        Filter();
    }

    /// <summary>"21/10/2026 · 3 times" - the last service it was in a schedule for, and how many services in all.</summary>
    public static string Sung(Song song) =>
        song.LastSungAt is { } last
            ? $"{ChurchTime.FromUtc(new DateTimeOffset(DateTime.SpecifyKind(last, DateTimeKind.Utc))):dd/MM/yyyy} · {(song.TimesSung == 1 ? "once" : $"{song.TimesSung} times")}"
            : "Not yet";

    /// <summary>Every word searched for must be in the name or somewhere in the words; the preview is the first line holding one.</summary>
    private void Filter()
    {
        string[] terms = Normalise(_search ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);

        IEnumerable<Song> songs = _songs.HasData ? _songs.Data! : [];

        if (_libraries.Count > 1)
            songs = songs.Where(x => _chosenLibraries.Contains(x.Library));

        _shown = [];
        _previews = [];

        foreach (Song song in songs)
        {
            List<string> lines = song.Groups
                .SelectMany(g => g.Slides)
                .SelectMany(s => s.Split('\n'))
                .Select(l => l.Trim())
                .Where(l => l.Length > 0)
                .ToList();

            string everything = Normalise($"{song.Name}\n{string.Join("\n", lines)}");

            if (!terms.All(everything.Contains))
                continue;

            _shown = _shown.Add(song);

            string? preview = terms.Length == 0
                ? lines.FirstOrDefault()
                : lines.FirstOrDefault(l => terms.Any(Normalise(l).Contains)) ?? lines.FirstOrDefault();

            if (preview != null)
                _previews[song.Id] = preview;
        }

        StateHasChanged();
    }

    /// <summary>
    /// Lower case, with apostrophes dropped - ProPresenter's curly ones and typed straight ones alike - so
    /// "He’s able" is found by "he's" and by "hes".
    /// </summary>
    private static string Normalise(string text) =>
        text.ToLowerInvariant().Replace("’", "").Replace("‘", "").Replace("'", "");
}
