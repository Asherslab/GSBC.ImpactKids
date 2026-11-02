using GSBC.ImpactKids.Shared.Contracts.Entities.Features.DataDisplay;

namespace GSBC.ImpactKids.Grpc.Data.Models.DataDisplay;

public class DbMetabaseEmbed
{
    public required Guid Id { get; set; }

    public required string Label { get; set; }

    /// <summary>Null for a folder.</summary>
    public int? MetabaseId { get; set; }

    public required MetabaseEmbedType Type { get; set; }

    /// <summary>The folder this is in; null at the top of the menu. Only ever a folder, and never set on one.</summary>
    public Guid? ParentId { get; set; }

    public required int SortOrder { get; set; }

    public bool IsFolder => Type == MetabaseEmbedType.Folder;
}
