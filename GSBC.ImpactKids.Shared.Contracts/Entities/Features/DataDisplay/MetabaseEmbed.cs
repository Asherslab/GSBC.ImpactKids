namespace GSBC.ImpactKids.Shared.Contracts.Entities.Features.DataDisplay;

/// <summary>
/// An entry of the Analytics menu: a Metabase dashboard or question shown on its own page, or a folder
/// grouping them. Managed on the admin page; the server signs an embed token only for a row that exists
/// here, never for an id the client names.
/// <para>
/// <b>One level of folders.</b> A folder sits at the top of the menu and holds dashboards and questions;
/// it never goes inside another folder.
/// </para>
/// </summary>
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public record MetabaseEmbed : IIdentifiable
{
    public required Guid Id { get; init; }

    /// <summary>The menu entry and the page title. Unique, because it is also the page's address.</summary>
    public required string Label { get; init; }

    /// <summary>The dashboard's or question's number in Metabase - the one in its own URL. Null for a folder.</summary>
    public int? MetabaseId { get; init; }

    public required MetabaseEmbedType Type { get; init; }

    /// <summary>The folder this is in; null at the top of the menu.</summary>
    public Guid? ParentId { get; init; }

    /// <summary>Its place among the entries sharing its <see cref="ParentId"/>.</summary>
    public required int SortOrder { get; init; }

    [ProtoIgnore]
    public bool IsFolder => Type == MetabaseEmbedType.Folder;

    /// <summary>What Metabase calls the type, both in the token's resource and in the embed URL.</summary>
    [ProtoIgnore]
    public string ResourceType => Type.ResourceType();
}

public enum MetabaseEmbedType
{
    Dashboard,
    Question,
    Folder
}

public static class MetabaseEmbedTypeExtensions
{
    public static string ResourceType(this MetabaseEmbedType type) => type switch
    {
        MetabaseEmbedType.Question => "question",
        _                          => "dashboard"
    };
}
