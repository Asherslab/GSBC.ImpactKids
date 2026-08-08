using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using EasyAppDev.Blazor.Store.Core;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Games;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Games;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.Shared.Contracts.Services.Features.Games;
using GSBC.ImpactKids.WASM.Services.RefreshableStore;
using Microsoft.JSInterop;

namespace GSBC.ImpactKids.WASM.Features.Games.Services;

/// <summary>
/// Offline first store for game points and board settings.
/// <para>
/// Points are append only deltas, so two devices scoring the same game while offline
/// merge on sync instead of overwriting each other. Every record carries a client
/// generated id, which makes resending a queued record idempotent server side.
/// </para>
/// <para>
/// Board settings are config rather than accumulating data, so those do use last
/// write wins - resolved by <see cref="GameBoard.UpdatedAt"/> on the server.
/// </para>
/// <para>
/// Everything is mirrored into browser storage, so a cold start with no reception
/// still shows the last known totals and can keep taking taps.
/// </para>
/// </summary>
public sealed class GamePointsService(
    IJSRuntime                         js,
    IServiceProvider                   services,
    IRefreshableStore<GamePointRecord> pointRecordsStore,
    IRefreshableStore<GameBoard>       boardsStore
) : IGamePointsService
{
    // v2: teams became a list on the board rather than a fixed four-value enum. Old
    // cached payloads would deserialize into the wrong shape, so they are left behind.
    private const string RecordsKey     = "gamepoints:v2:records";
    private const string OutboxKey      = "gamepoints:v2:outbox";
    private const string DeletesKey     = "gamepoints:v2:deletes";
    private const string BoardsKey      = "gamepoints:v2:boards";
    private const string BoardOutboxKey = "gamepoints:v2:boardoutbox";

    private static readonly TimeSpan PruneAfter = TimeSpan.FromDays(90);
    private static readonly TimeSpan RetryEvery = TimeSpan.FromSeconds(20);

    /// <summary>Known records, server sourced and local, keyed by id.</summary>
    private readonly Dictionary<Guid, GamePointRecord> _records = [];

    /// <summary>Effective board per service - local edits applied over server state.</summary>
    private readonly Dictionary<Guid, GameBoard> _boards = [];

    /// <summary>Creates that have not been acknowledged by the server yet.</summary>
    private readonly Dictionary<Guid, CreateGamePointRecordRequest> _outbox = [];

    /// <summary>Deletes that have not been acknowledged by the server yet.</summary>
    private readonly HashSet<Guid> _pendingDeletes = [];

    /// <summary>Board edits not yet acknowledged, keyed by service.</summary>
    private readonly Dictionary<Guid, UpsertGameBoardRequest> _boardOutbox = [];

    private IJSObjectReference?                       _module;
    private DotNetObjectReference<GamePointsService>? _selfRef;
    private CancellationTokenSource?                  _retryTokenSource;
    private IDisposable?                              _recordsSubscription;
    private IDisposable?                              _boardsSubscription;

    private bool _flushing;

    public event Action? Changed;

    public bool IsOnline    { get; private set; } = true;
    public bool Initialised { get; private set; }

    public int PendingCount => _outbox.Count + _pendingDeletes.Count + _boardOutbox.Count;

    public async Task InitialiseAsync()
    {
        if (Initialised)
        {
            // Already warm - still worth a flush in case a previous attempt failed.
            _ = FlushAsync();
            return;
        }

        await LoadFromStorageAsync();

        _recordsSubscription = pointRecordsStore.Subscribe(OnServerRecordsChanged);
        _boardsSubscription = boardsStore.Subscribe(OnServerBoardsChanged);

        MergeRecordsFromServer();
        MergeBoardsFromServer();

        await StartConnectivityWatchAsync();

        Initialised = true;
        Changed?.Invoke();

        StartRetryLoop();

        // Kick off server reads; failures are expected and harmless when offline.
        _ = RefreshFromServerAsync();
    }

    private void OnServerRecordsChanged(EntityListState<GamePointRecord> state)
    {
        MergeRecordsFromServer();
        _ = PersistRecordsAsync();
        Changed?.Invoke();
    }

    private void OnServerBoardsChanged(EntityListState<GameBoard> state)
    {
        MergeBoardsFromServer();
        _ = PersistBoardsAsync();
        Changed?.Invoke();
    }

    private async Task RefreshFromServerAsync()
    {
        try
        {
            await Task.WhenAll(
                pointRecordsStore.RefreshAll(),
                boardsStore.RefreshAll()
            );
        }
        catch
        {
            // Offline, or the server is unreachable. Local state stands.
        }
    }

    // ---------- board ----------

    public GameBoard BoardFor(Guid serviceId) =>
        _boards.TryGetValue(serviceId, out GameBoard? board)
            ? board
            : GameBoard.Default(serviceId);

    public async Task UpdateBoardAsync(Guid serviceId, Func<GameBoard, GameBoard> mutate)
    {
        GameBoard updated = mutate(BoardFor(serviceId)) with { UpdatedAt = DateTime.UtcNow };

        _boards[serviceId] = updated;

        _boardOutbox[serviceId] = new UpsertGameBoardRequest
        {
            ServiceId = serviceId,
            CurrentGame = updated.CurrentGame,
            Teams = updated.Teams,
            Games = updated.Games,
            StepPoints = updated.StepPoints,
            BonusPoints = updated.BonusPoints,
            DisplayMode = updated.DisplayMode,
            Hidden = updated.Hidden,
            Paused = updated.Paused,
            PausedAt = updated.PausedAt,
            UpdatedAt = updated.UpdatedAt
        };

        Changed?.Invoke();

        await PersistBoardsAsync();
        await PersistBoardOutboxAsync();

        _ = FlushAsync();
    }

    // ---------- reads ----------

    public int GamesPlayed(Guid serviceId)
    {
        int highestScoredIn = _records.Values
            .Where(x => !x.Deleted && x.ServiceId == serviceId && x.GameNumber != null)
            .Select(x => x.GameNumber!.Value)
            .DefaultIfEmpty(0)
            .Max();

        return Math.Max(BoardFor(serviceId).CurrentGame, highestScoredIn);
    }

    public bool HasScores(Guid serviceId, int gameNumber) =>
        _records.Values.Any(x => !x.Deleted && x.ServiceId == serviceId && x.GameNumber == gameNumber);

    public int TotalFor(Guid serviceId, int teamIndex) =>
        Live(serviceId, teamIndex).Sum(x => x.Points);

    public int GamePointsFor(Guid serviceId, int teamIndex) =>
        Live(serviceId, teamIndex).Where(x => x.GameNumber != null).Sum(x => x.Points);

    public int GamePointsFor(Guid serviceId, int teamIndex, int gameNumber) =>
        Live(serviceId, teamIndex).Where(x => x.GameNumber == gameNumber).Sum(x => x.Points);

    public int BehaviourPointsFor(Guid serviceId, int teamIndex) =>
        Live(serviceId, teamIndex).Where(x => x.GameNumber == null).Sum(x => x.Points);

    private IEnumerable<GamePointRecord> Live(Guid serviceId, int teamIndex) =>
        _records.Values.Where(x => !x.Deleted && x.ServiceId == serviceId && x.TeamIndex == teamIndex);

    public bool CanUndo(Guid serviceId) => LastRecordFor(serviceId) != null;

    private GamePointRecord? LastRecordFor(Guid serviceId) =>
        _records.Values
            .Where(x => !x.Deleted && x.ServiceId == serviceId)
            .MaxBy(x => x.Awarded);

    // ---------- writes ----------

    public Task AddGamePointsAsync(Guid serviceId, IReadOnlyList<int> teamIndexes, int points) =>
        AddPointsAsync(serviceId, teamIndexes, points, BoardFor(serviceId).CurrentGame);

    public Task AddBehaviourPointsAsync(Guid serviceId, int teamIndex, int points) =>
        AddPointsAsync(serviceId, [teamIndex], points, gameNumber: null);

    /// <summary>
    /// One record per team. A combined side therefore scores the full amount for each of
    /// its teams, and the shared group id keeps them together for undo.
    /// </summary>
    private async Task AddPointsAsync(
        Guid               serviceId,
        IReadOnlyList<int> teamIndexes,
        int                points,
        int?               gameNumber
    )
    {
        if (points == 0 || teamIndexes.Count == 0)
            return;

        DateTime awarded = DateTime.UtcNow;
        Guid?    groupId = teamIndexes.Count > 1 ? Guid.NewGuid() : null;

        foreach (int teamIndex in teamIndexes.Distinct())
        {
            Guid id = Guid.NewGuid();

            // Applied locally first so the tap registers instantly, online or not.
            _records[id] = new GamePointRecord
            {
                Id = id,
                TeamIndex = teamIndex,
                Points = points,
                GameNumber = gameNumber,
                GroupId = groupId,
                Awarded = awarded,
                ServiceId = serviceId
            };

            _outbox[id] = new CreateGamePointRecordRequest
            {
                Id = id,
                TeamIndex = teamIndex,
                Points = points,
                GameNumber = gameNumber,
                GroupId = groupId,
                Awarded = awarded,
                ServiceId = serviceId
            };
        }

        Changed?.Invoke();

        await PersistRecordsAsync();
        await PersistOutboxAsync();

        _ = Vibrate(12);
        _ = FlushAsync();
    }

    public async Task UndoLastAsync(Guid serviceId)
    {
        GamePointRecord? last = LastRecordFor(serviceId);

        if (last == null)
            return;

        // A combined side was scored as several records at once, so undo has to take the
        // whole award back rather than leaving one team ahead.
        List<GamePointRecord> undoing = last.GroupId == null
            ? [last]
            : _records.Values
                .Where(x => !x.Deleted && x.ServiceId == serviceId && x.GroupId == last.GroupId)
                .ToList();

        foreach (GamePointRecord record in undoing)
        {
            if (_outbox.Remove(record.Id))
            {
                // Never reached the server, so drop it outright rather than tombstoning it.
                _records.Remove(record.Id);
            }
            else
            {
                _records[record.Id] = record with { Deleted = true };
                _pendingDeletes.Add(record.Id);
            }
        }

        Changed?.Invoke();

        await PersistRecordsAsync();
        await PersistOutboxAsync();
        await PersistDeletesAsync();

        _ = Vibrate(25);
        _ = FlushAsync();
    }

    // ---------- sync ----------

    public async Task FlushAsync()
    {
        if (_flushing || !IsOnline || PendingCount == 0)
            return;

        _flushing = true;

        try
        {
            using IServiceScope scope = services.CreateScope();

            IGamePointRecordService recordService =
                scope.ServiceProvider.GetRequiredService<IGamePointRecordService>();
            IGameBoardService boardService =
                scope.ServiceProvider.GetRequiredService<IGameBoardService>();

            bool changed = false;

            foreach (CreateGamePointRecordRequest request in _outbox.Values.ToList())
            {
                SendOutcome outcome = await TrySendCreateAsync(recordService, request);

                if (outcome == SendOutcome.Unreachable)
                    break; // Network is down again - keep the rest queued in order.

                if (outcome == SendOutcome.Rejected)
                {
                    // The server will never take this one. Drop the local copy too so
                    // this device does not show a total the others will never agree with.
                    _records.Remove(request.Id);
                    await PersistRecordsAsync();
                }

                _outbox.Remove(request.Id);
                changed = true;
            }

            foreach (Guid id in _pendingDeletes.ToList())
            {
                if (await TrySendDeleteAsync(recordService, id) == SendOutcome.Unreachable)
                    break;

                _pendingDeletes.Remove(id);
                changed = true;
            }

            if (changed)
            {
                await PersistOutboxAsync();
                await PersistDeletesAsync();
            }

            bool boardsChanged = false;

            foreach (UpsertGameBoardRequest request in _boardOutbox.Values.ToList())
            {
                if (await TrySendBoardAsync(boardService, request) == SendOutcome.Unreachable)
                    break;

                _boardOutbox.Remove(request.ServiceId);
                boardsChanged = true;
            }

            if (boardsChanged)
                await PersistBoardOutboxAsync();

            if (changed || boardsChanged)
            {
                Changed?.Invoke();
                await RefreshFromServerAsync();
            }
        }
        finally
        {
            _flushing = false;
        }
    }

    private enum SendOutcome
    {
        /// <summary>Could not reach the server - retry later, keep the queue intact.</summary>
        Unreachable,

        /// <summary>The server took it (or already had it).</summary>
        Accepted,

        /// <summary>The server refused it - retrying would wedge the queue forever.</summary>
        Rejected
    }

    private static async Task<SendOutcome> TrySendCreateAsync(
        IGamePointRecordService      service,
        CreateGamePointRecordRequest request
    )
    {
        try
        {
            BasicReadResponse<Guid?> resp = await service.Create(request);

            // The exception interceptor swallows auth failures into a null response.
            if (resp is null)
                return SendOutcome.Unreachable;

            return resp.Success ? SendOutcome.Accepted : SendOutcome.Rejected;
        }
        catch
        {
            return SendOutcome.Unreachable;
        }
    }

    private static async Task<SendOutcome> TrySendDeleteAsync(IGamePointRecordService service, Guid id)
    {
        try
        {
            BasicResponse resp = await service.BasicDelete(new BasicReadRequest { Guid = id });

            if (resp is null)
                return SendOutcome.Unreachable;

            return resp.Success ? SendOutcome.Accepted : SendOutcome.Rejected;
        }
        catch
        {
            return SendOutcome.Unreachable;
        }
    }

    private static async Task<SendOutcome> TrySendBoardAsync(
        IGameBoardService      service,
        UpsertGameBoardRequest request
    )
    {
        try
        {
            BasicReadResponse<Guid?> resp = await service.Create(request);

            if (resp is null)
                return SendOutcome.Unreachable;

            return resp.Success ? SendOutcome.Accepted : SendOutcome.Rejected;
        }
        catch
        {
            return SendOutcome.Unreachable;
        }
    }

    private void MergeRecordsFromServer()
    {
        ImmutableList<GamePointRecord>? serverRecords = pointRecordsStore.GetState().Entities.Data;

        if (serverRecords == null)
            return;

        foreach (GamePointRecord record in serverRecords)
        {
            // The server is authoritative for anything it has seen.
            _records[record.Id] = record;

            // Seeing our own record come back means the create landed, even if we
            // never got the acknowledgement.
            _outbox.Remove(record.Id);

            if (record.Deleted)
                _pendingDeletes.Remove(record.Id);
        }
    }

    private void MergeBoardsFromServer()
    {
        ImmutableList<GameBoard>? serverBoards = boardsStore.GetState().Entities.Data;

        if (serverBoards == null)
            return;

        foreach (GameBoard board in serverBoards)
        {
            // Keep a local edit that is newer than what the server has - it is still
            // queued and will win once it lands.
            if (_boards.TryGetValue(board.ServiceId, out GameBoard? local) && local.UpdatedAt > board.UpdatedAt)
                continue;

            _boards[board.ServiceId] = board;

            if (_boardOutbox.TryGetValue(board.ServiceId, out UpsertGameBoardRequest? queued) &&
                queued.UpdatedAt <= board.UpdatedAt)
                _boardOutbox.Remove(board.ServiceId);
        }
    }

    // ---------- connectivity ----------

    private async Task StartConnectivityWatchAsync()
    {
        try
        {
            _module ??= await js.InvokeAsync<IJSObjectReference>("import", "./js/connectivity.js");
            _selfRef ??= DotNetObjectReference.Create(this);
            IsOnline = await _module.InvokeAsync<bool>("start", _selfRef);
        }
        catch
        {
            // Assume online if the browser will not tell us; sends will fail loudly enough.
            IsOnline = true;
        }
    }

    [JSInvokable]
    public async Task OnConnectivityChanged(bool online)
    {
        IsOnline = online;
        Changed?.Invoke();

        if (online)
            await FlushAsync();
    }

    private void StartRetryLoop()
    {
        _retryTokenSource?.Cancel();
        _retryTokenSource = new CancellationTokenSource();
        CancellationToken token = _retryTokenSource.Token;

        _ = Task.Run(async () =>
            {
                using PeriodicTimer timer = new(RetryEvery);

                while (await timer.WaitForNextTickAsync(token))
                {
                    if (PendingCount > 0)
                        await FlushAsync();
                }
            },
            token
        );
    }

    private async Task Vibrate(int milliseconds)
    {
        try
        {
            if (_module is not null)
                await _module.InvokeVoidAsync("vibrate", milliseconds);
        }
        catch
        {
            // Haptics are decoration.
        }
    }

    // ---------- browser storage ----------

    private async Task LoadFromStorageAsync()
    {
        DateTime cutoff = DateTime.UtcNow - PruneAfter;

        foreach (GamePointRecord record in await ReadListAsync(RecordsKey, GamesJsonContext.Default.ListGamePointRecord))
        {
            if (record.Awarded >= cutoff)
                _records[record.Id] = record;
        }

        foreach (GameBoard board in await ReadListAsync(BoardsKey, GamesJsonContext.Default.ListGameBoard))
        {
            _boards[board.ServiceId] = board;
        }

        foreach (CreateGamePointRecordRequest request in
                 await ReadListAsync(OutboxKey, GamesJsonContext.Default.ListCreateGamePointRecordRequest))
        {
            _outbox[request.Id] = request;
        }

        foreach (UpsertGameBoardRequest request in
                 await ReadListAsync(BoardOutboxKey, GamesJsonContext.Default.ListUpsertGameBoardRequest))
        {
            _boardOutbox[request.ServiceId] = request;
        }

        foreach (Guid id in await ReadListAsync(DeletesKey, GamesJsonContext.Default.ListGuid))
        {
            _pendingDeletes.Add(id);
        }
    }

    private Task PersistRecordsAsync() => WriteListAsync(
        RecordsKey,
        _records.Values.ToList(),
        GamesJsonContext.Default.ListGamePointRecord
    );

    private Task PersistBoardsAsync() => WriteListAsync(
        BoardsKey,
        _boards.Values.ToList(),
        GamesJsonContext.Default.ListGameBoard
    );

    private Task PersistOutboxAsync() => WriteListAsync(
        OutboxKey,
        _outbox.Values.ToList(),
        GamesJsonContext.Default.ListCreateGamePointRecordRequest
    );

    private Task PersistBoardOutboxAsync() => WriteListAsync(
        BoardOutboxKey,
        _boardOutbox.Values.ToList(),
        GamesJsonContext.Default.ListUpsertGameBoardRequest
    );

    private Task PersistDeletesAsync() => WriteListAsync(
        DeletesKey,
        _pendingDeletes.ToList(),
        GamesJsonContext.Default.ListGuid
    );

    private async Task<List<T>> ReadListAsync<T>(string key, JsonTypeInfo<List<T>> typeInfo)
    {
        try
        {
            string? raw = await ReadAsync(key);

            if (string.IsNullOrWhiteSpace(raw))
                return [];

            return JsonSerializer.Deserialize(raw, typeInfo) ?? [];
        }
        catch
        {
            // Corrupt or unreadable storage should never stop the tracker opening.
            return [];
        }
    }

    private async Task WriteListAsync<T>(string key, List<T> values, JsonTypeInfo<List<T>> typeInfo)
    {
        try
        {
            await WriteAsync(key, JsonSerializer.Serialize(values, typeInfo));
        }
        catch
        {
            // Storage full or blocked - in memory state still works for this session.
        }
    }

    private async Task<string?> ReadAsync(string key)
    {
        try
        {
            return await js.InvokeAsync<string?>("localStorage.getItem", key);
        }
        catch
        {
            return null;
        }
    }

    private async Task WriteAsync(string key, string value)
    {
        try
        {
            await js.InvokeVoidAsync("localStorage.setItem", key, value);
        }
        catch
        {
            // ignored
        }
    }

    public async ValueTask DisposeAsync()
    {
        _recordsSubscription?.Dispose();
        _boardsSubscription?.Dispose();

        if (_retryTokenSource is not null)
        {
            await _retryTokenSource.CancelAsync();
            _retryTokenSource.Dispose();
            _retryTokenSource = null;
        }

        if (_module is not null)
        {
            try
            {
                await _module.InvokeVoidAsync("stop");
                await _module.DisposeAsync();
            }
            catch
            {
                // ignored
            }

            _module = null;
        }

        _selfRef?.Dispose();
        _selfRef = null;
    }
}
