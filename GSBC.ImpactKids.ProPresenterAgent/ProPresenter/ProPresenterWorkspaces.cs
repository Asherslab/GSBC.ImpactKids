using System.Net;
using Microsoft.Data.Sqlite;

namespace GSBC.ImpactKids.ProPresenterAgent.ProPresenter;

/// <summary>
/// ProPresenter's workspaces on this Mac, and which one is open - read from ProPresenter's own registry
/// on disk, because the network API has no route for either (2026-10-23, ProPresenter 22: no
/// <c>/v1/workspace</c> or anything like it) and shows only the open workspace's libraries.
/// <para>
/// <b>The song sync cannot work without this.</b> A library's uuid is derived from its name, so every
/// workspace's "Songs" library has the same uuid; going by libraries alone, the song library would
/// follow ProPresenter into whichever workspace was opened, and empty itself in one without songs.
/// </para>
/// <para>
/// The registry is a SQLite file ProPresenter keeps open, opened here read-only and never cached. Only
/// meaningful when ProPresenter runs on this Mac: for any other address the open workspace is unknown.
/// </para>
/// </summary>
public sealed class ProPresenterWorkspaces(AgentState state, ILogger<ProPresenterWorkspaces> logger)
{
    private static readonly string RegistryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Library", "Application Support", "RenewedVision", "ProPresenter", "Workspaces", "registry-v1.sqlite3"
    );

    public sealed record Workspace(string Uuid, string Name);

    public sealed record Registry(List<Workspace> Workspaces, string? ActiveUuid);

    /// <summary>The workspaces and the open one, or null when the registry cannot be read or ProPresenter is on another computer.</summary>
    public Registry? Read()
    {
        if (state.Configuration is not { } configuration || !IsThisMac(configuration.ProPresenterHost))
            return null;

        if (!File.Exists(RegistryPath))
            return null;

        try
        {
            // No pooling: a pooled connection would hold ProPresenter's file open between ticks.
            using SqliteConnection connection = new($"Data Source={RegistryPath};Mode=ReadOnly;Pooling=False");
            connection.Open();

            List<Workspace> workspaces = [];

            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = "SELECT local_uuid, display_name FROM workspaces ORDER BY display_name";

                using SqliteDataReader reader = command.ExecuteReader();

                while (reader.Read())
                    workspaces.Add(new Workspace(reader.GetString(0), reader.GetString(1)));
            }

            using SqliteCommand active = connection.CreateCommand();
            active.CommandText = "SELECT value FROM meta WHERE key = 'active_local_uuid'";

            return new Registry(workspaces, active.ExecuteScalar() as string);
        }
        catch (SqliteException exception)
        {
            logger.LogWarning("Could not read ProPresenter's workspaces: {Message}", exception.Message);
            return null;
        }
    }

    private static bool IsThisMac(string host) =>
        host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        || host.Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase)
        || host.Equals($"{Environment.MachineName}.local", StringComparison.OrdinalIgnoreCase)
        || (IPAddress.TryParse(host, out IPAddress? address) && IPAddress.IsLoopback(address));
}
