using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.DataDisplay;

namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.DataDisplay.MetabaseEmbeds;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class UpdateMetabaseEmbedRequest : ReadRequestBase, IUpdateRequest<MetabaseEmbed, UpdateMetabaseEmbedRequest>
{
    public override string Id { get; set; } = null!;

    public DeltaUpdate<string> Label { get; set; } = new();

    public DeltaUpdate<int> MetabaseId { get; set; } = new();

    public DeltaUpdate<MetabaseEmbedType> Type { get; set; } = new();

    public static UpdateMetabaseEmbedRequest FromEntity(MetabaseEmbed entity)
    {
        UpdateMetabaseEmbedRequest request = new()
        {
            Guid = entity.Id
        };

        request.Label.SetInitialValue(entity.Label);
        request.MetabaseId.SetInitialValue(entity.MetabaseId ?? 0);
        request.Type.SetInitialValue(entity.Type);
        return request;
    }
}
