namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.DataDisplay.MetabaseEmbeds;

/// <summary>A drag on the admin page: into a folder, or to the top of the menu when null, at a position among its entries.</summary>
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class MoveMetabaseEmbedRequest
{
    public Guid Id { get; set; }

    public Guid? ParentId { get; set; }

    public int Position { get; set; }
}
