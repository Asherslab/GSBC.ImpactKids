using GSBC.ImpactKids.Shared.Contracts.Entities.Features.DataDisplay;

namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.DataDisplay.MetabaseEmbeds;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class CreateMetabaseEmbedRequest
{
    public string? Label { get; set; }

    public int MetabaseId { get; set; }

    public MetabaseEmbedType Type { get; set; } = MetabaseEmbedType.Dashboard;
}
