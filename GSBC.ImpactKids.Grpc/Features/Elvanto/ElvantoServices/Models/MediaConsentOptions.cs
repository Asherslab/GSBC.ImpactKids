namespace GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Models;

/// <summary>
/// The option ids behind Elvanto's "Media Photo Consent Given" custom field.
///
/// Reads return the option as <c>{id, name}</c>, so the name alone is enough to interpret one.
/// Writes are not symmetrical: a <c>select</c> custom field wants the option <b>id</b>, inside an
/// array. Sending the name instead is rejected with "Invalid Value for custom field
/// custom_196785e4-…", which is how this was found - the first real create attempt failed on it
/// and nothing was written.
///
/// Sourced from <c>people/customFields/getAll</c>, which is the authority if these ever move.
/// Kept as constants alongside the custom field ids on <see cref="ElvantoPerson"/> rather than
/// fetched per run: they identify rows in the church's own Elvanto account and change about as
/// often as the field itself.
/// </summary>
public static class MediaConsentOptions
{
    private static readonly Dictionary<string, string> IdByName = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Not Requested"] = "409f089c-f6b9-4ab0-a126-ff93a6a8ac05",
        ["Yes"]           = "5aea4436-6888-49a8-8790-e10fcdac86d5",
        ["No"]            = "451e31fa-2eec-4f3e-baf1-94bd007ed8c8",
        ["Strictly No"]   = "c1cf6eca-b30c-40af-926d-a9ffce97e366"
    };

    /// <summary>
    /// The option id for a display name, or the name itself when it is not one of the four.
    /// Falling back to the name means Elvanto refuses the write and says so; returning null would
    /// quietly drop the field instead, and silently losing a photo-consent answer is the worse
    /// failure of the two.
    /// </summary>
    public static string IdForName(string name) =>
        IdByName.TryGetValue(name.Trim(), out string? id) ? id : name;

    /// <summary>
    /// The display name for an option id, for reading a value back out of a written payload.
    /// </summary>
    public static string? NameForId(string id) =>
        IdByName.FirstOrDefault(kv => string.Equals(kv.Value, id, StringComparison.OrdinalIgnoreCase)).Key;
}
