namespace GSBC.ImpactKids.Shared.Contracts.Entities.Features.DataDisplay;

/// <summary>
/// A Metabase dashboard or question shown on its own page under Analytics. Managed on the admin page;
/// the server signs an embed token only for a row that exists here, never for an id the client names.
/// </summary>
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public record MetabaseEmbed : IIdentifiable
{
    public required Guid Id { get; init; }

    /// <summary>The menu entry and the page title. Unique, because it is also the page's address.</summary>
    public required string Label { get; init; }

    /// <summary>The dashboard's or question's number in Metabase - the one in its own URL.</summary>
    public required int MetabaseId { get; init; }

    public required MetabaseEmbedType Type { get; init; }

    /// <summary>What Metabase calls the type, both in the token's resource and in the embed URL.</summary>
    [ProtoIgnore]
    public string ResourceType => Type.ResourceType();
}

public enum MetabaseEmbedType
{
    Dashboard,
    Question
}

public static class MetabaseEmbedTypeExtensions
{
    public static string ResourceType(this MetabaseEmbedType type) => type switch
    {
        MetabaseEmbedType.Question => "question",
        _                          => "dashboard"
    };
}
