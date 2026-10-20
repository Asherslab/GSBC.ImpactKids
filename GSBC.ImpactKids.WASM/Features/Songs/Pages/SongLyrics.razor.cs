using System.Collections.Immutable;
using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Songs;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.Songs.Pages;

/// <summary>
/// One song's words, in the order they are sung. Opens on an arrangement - see
/// <see cref="OpeningArrangement"/> - and the others are a tap away.
/// </summary>
public partial class SongLyrics
{
    /// <summary>Not an arrangement uuid, so <see cref="Song.Sung"/> gives every group once.</summary>
    private const string EveryGroupOnce = "";

    [Parameter]
    public Guid Id { get; set; }

    private AsyncData<ImmutableList<Song>> _songs = AsyncData<ImmutableList<Song>>.NotAsked();

    private Song?  _song;
    private string _arrangement = EveryGroupOnce;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        HandleSubscriptionDisposal(SongsStore, RetrieveSong);
        RetrieveSong();

        await SongsStore.RefreshAll();
    }

    protected override void OnParametersSet() => RetrieveSong();

    private void RetrieveSong()
    {
        _songs = SongsStore.GetState().Entities;

        Song? song = _songs.HasData ? _songs.Data!.FirstOrDefault(x => x.Id == Id) : null;

        // A different song, or the first time it loads: start on an arrangement. The same song
        // refreshed - its words changed in ProPresenter - keeps the arrangement being read.
        if (song?.Id != _song?.Id)
            _arrangement = song == null ? EveryGroupOnce : OpeningArrangement(song);

        _song = song;

        StateHasChanged();
    }

    /// <summary>
    /// An arrangement is the order a song is actually sung in, where every part once is not - so a song
    /// opens on one whenever it has one: ProPresenter's own choice, else the one called "Default", else
    /// the first. Every part once only for a song with no arrangements at all.
    /// </summary>
    private static string OpeningArrangement(Song song) =>
        song.Arrangements.FirstOrDefault(x => x.Uuid == song.DefaultArrangementUuid)?.Uuid
        ?? song.Arrangements.FirstOrDefault(x => x.Name == "Default")?.Uuid
        ?? song.Arrangements.FirstOrDefault()?.Uuid
        ?? EveryGroupOnce;
}
