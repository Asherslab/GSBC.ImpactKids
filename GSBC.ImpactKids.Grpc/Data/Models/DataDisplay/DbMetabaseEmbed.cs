using GSBC.ImpactKids.Shared.Contracts.Entities.Features.DataDisplay;

namespace GSBC.ImpactKids.Grpc.Data.Models.DataDisplay;

public class DbMetabaseEmbed
{
    public required Guid Id { get; set; }

    public required string Label { get; set; }

    public required int MetabaseId { get; set; }

    public required MetabaseEmbedType Type { get; set; }
}
