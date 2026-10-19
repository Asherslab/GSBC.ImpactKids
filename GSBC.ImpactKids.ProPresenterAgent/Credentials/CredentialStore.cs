using System.Text.Json;
using Microsoft.Extensions.Options;

namespace GSBC.ImpactKids.ProPresenterAgent.Credentials;

public sealed record MachineCredential(Guid MachineId, string Secret);

/// <summary>
/// The machine's credential, in a file only this user can read.
/// <para>
/// <b>Not the Keychain, on purpose.</b> A Keychain item's access list is tied to the code signature
/// of the app that created it, and these builds are ad-hoc signed, so every release is a "different
/// app": the first read after an upgrade raises a modal "allow access?" prompt, and an agent that
/// starts at login on an unattended Mac sits blocked behind it until somebody walks past. The only
/// way to avoid the prompt is an item any app may read, which protects nothing a <c>0600</c> file in
/// the user's own Library does not.
/// </para>
/// <para>
/// One credential per Impact Kids address, so a developer's local run against the AppHost never
/// overwrites the installed agent's production credential.
/// </para>
/// </summary>
public sealed class CredentialStore(IOptions<AgentOptions> options, ILogger<CredentialStore> logger)
{
    private string FilePath => Path.Combine(
        options.Value.ResolvedDataDirectory,
        $"credential-{options.Value.ImpactKidsUrl.Host}-{options.Value.ImpactKidsUrl.Port}.json"
    );

    public MachineCredential? Load()
    {
        if (!File.Exists(FilePath))
            return null;

        try
        {
            return JsonSerializer.Deserialize<MachineCredential>(File.ReadAllText(FilePath));
        }
        catch (JsonException)
        {
            // Unreadable is the same as absent: the machine sets itself up again, and the old row is
            // deleted from the admin page.
            logger.LogWarning("The saved credential at {Path} could not be read; setting up again", FilePath);
            return null;
        }
    }

    public void Save(MachineCredential credential)
    {
        string directory = options.Value.ResolvedDataDirectory;

        Directory.CreateDirectory(directory);
        File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        // Created with its final permissions before any secret is written into it, so there is no
        // moment where the secret sits in a file somebody else could read.
        string temporary = FilePath + ".tmp";

        using (FileStream stream = new(temporary, new FileStreamOptions
               {
                   Mode = FileMode.Create,
                   Access = FileAccess.Write,
                   UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite
               }))
        {
            JsonSerializer.Serialize(stream, credential);
        }

        File.Move(temporary, FilePath, overwrite: true);
    }

    public void Delete()
    {
        if (File.Exists(FilePath))
            File.Delete(FilePath);
    }
}
