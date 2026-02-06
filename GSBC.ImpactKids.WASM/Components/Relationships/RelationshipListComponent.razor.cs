using GSBC.ImpactKids.Shared.Contracts.Entities.Interfaces;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.WASM.Extensions;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Components.Relationships;

public partial class RelationshipListComponent<FirstEntity, SecondEntity, EntityBeingAddedTo, EntityToAdd>
    where FirstEntity : class, IIdentifiable
    where SecondEntity : class, IIdentifiable
    where EntityBeingAddedTo : class
    where EntityToAdd : class, IIdentifiable
{
    [Parameter, EditorRequired]
    public EntityBeingAddedTo? Entity { get; set; }

    [Parameter]
    public string? Title { get; set; }

    [Parameter]
    public string? EntityToAddLabel { get; set; }

    [Parameter]
    public required RenderFragment<RelationshipListEntry<FirstEntity, SecondEntity, EntityBeingAddedTo, EntityToAdd>>
        ChildContent { get; set; }

    [Parameter]
    public Func<EntityToAdd, bool>? ListFilter { get; set; }

    [Parameter]
    public required Func<EntityToAdd?, string?> GetSearchDisplayFunc { get; set; }

    [Parameter]
    public required Func<string?, IEnumerable<EntityToAdd>, IEnumerable<EntityToAdd>> SearchFunc { get; set; }

    private async Task AddEntityDialog() => await RelationshipAddDialog<FirstEntity, SecondEntity, EntityToAdd>.Open(
        DialogService,
        $"Add {EntityToAddLabel ?? "Relationship"}",
        EntityToAddLabel,
        GetSearchDisplayFunc,
        SearchFunc,
        Entity
    );

    private Guid GetFirstEntityId(Guid id) => Entity is FirstEntity entityBeingAddedTo
        ? entityBeingAddedTo.Id
        : id;

    private Guid GetSecondEntityId(Guid id) => Entity is SecondEntity entityBeingAddedTo
        ? entityBeingAddedTo.Id
        : id;

    public async Task RemoveEntity(Guid id)
    {
        if (Entity == null)
            return;

        BasicResponse resp = await RelationshipService.DeleteRelationship(
            new BasicMultipleRelationshipRequest<FirstEntity, SecondEntity>
            {
                FirstId = GetFirstEntityId(id),
                SecondId = GetSecondEntityId(id)
            });

        if (resp.HasErrorOrNull())
            Snackbar.AddErrorResponse(resp);
    }
}

public record RelationshipListEntry<FirstEntity, SecondEntity, EntityBeingAddedTo, EntityToAdd>(
    Guid?                                                                                 Id,
    RelationshipListComponent<FirstEntity, SecondEntity, EntityBeingAddedTo, EntityToAdd> Component
)
    where FirstEntity : class, IIdentifiable
    where SecondEntity : class, IIdentifiable
    where EntityBeingAddedTo : class
    where EntityToAdd : class, IIdentifiable;