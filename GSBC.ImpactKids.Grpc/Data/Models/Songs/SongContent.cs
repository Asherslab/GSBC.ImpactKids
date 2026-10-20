using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Songs;

namespace GSBC.ImpactKids.Grpc.Data.Models.Songs;

/// <summary>What <see cref="DbSong.ContentJson"/> holds: everything about a song except its name and library.</summary>
public record SongContent(
    ImmutableList<SongGroup>       Groups,
    ImmutableList<SongArrangement> Arrangements,
    string?                        DefaultArrangementUuid
)
{
    public string ToJson() => JsonSerializer.Serialize(this);

    public static SongContent FromJson(string json) =>
        JsonSerializer.Deserialize<SongContent>(json) ?? new SongContent([], [], null);

    /// <summary>The hash a sync compares to tell a changed song from one sent again unchanged.</summary>
    public static string Hash(string name, string library, string contentJson) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes($"{name}\n{library}\n{contentJson}")));
}
