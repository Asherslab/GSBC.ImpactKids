using GSBC.ImpactKids.Shared.Contracts.Entities.Interfaces;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.WASM.Extensions;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace GSBC.ImpactKids.WASM.Components.Relationships;

public partial class RelationshipAddDialog<FirstEntity, SecondEntity, EntityToAdd>
    where FirstEntity : class, IIdentifiable
    where SecondEntity : class, IIdentifiable
    where EntityToAdd : class
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter]
    public string? DialogLabel { get; set; }

    [Parameter]
    public required Func<EntityToAdd?, string?> GetSearchDisplayFunc { get; set; }

    [Parameter]
    public required Func<string?, IEnumerable<EntityToAdd>, IEnumerable<EntityToAdd>> SearchFunc { get; set; }

    [Parameter]
    public object? EntityBeingAddedTo { get; set; }

    private EntityToAdd? _entityToAdd;

    private void EntitySelected(EntityToAdd entity)
    {
        _entityToAdd = entity;
    }

    private Task<IEnumerable<EntityToAdd>> EntitySearch(string? search, CancellationToken token = default)
    {
        if (!EntityToAddStore.GetState().Entities.HasData)
            return Task.FromResult<IEnumerable<EntityToAdd>>([]);

        return Task.FromResult(SearchFunc(search, EntityToAddStore.GetState().Entities.Data!));
    }

    private FirstEntity? GetFirstEntity() => _entityToAdd as FirstEntity ?? EntityBeingAddedTo as FirstEntity;

    private SecondEntity? GetSecondEntity() => _entityToAdd as SecondEntity ?? EntityBeingAddedTo as SecondEntity;

    private async Task AddRelationship()
    {
        if (EntityBeingAddedTo == null || _entityToAdd == null)
            return;

        FirstEntity?  firstEntity  = GetFirstEntity();
        SecondEntity? secondEntity = GetSecondEntity();

        if (firstEntity == null || secondEntity == null)
            return;

        BasicResponse resp = await RelationshipService.CreateRelationship(
            new BasicMultipleRelationshipRequest<FirstEntity, SecondEntity>
            {
                FirstId = firstEntity.Id,
                SecondId = secondEntity.Id
            });

        _entityToAdd = null;
        if (resp.HasErrorOrNull())
        {
            Snackbar.AddErrorResponse(resp);
            return;
        }

        MudDialog.Close();
    }

    public static async Task<DialogResult?> Open(
        IDialogService                                                    dialogService,
        string                                                            title,
        string?                                                           dialogLabel,
        Func<EntityToAdd?, string?>                                       getSearchDisplayFunc,
        Func<string?, IEnumerable<EntityToAdd>, IEnumerable<EntityToAdd>> searchFunc,
        object?                                                           entityBeingAddedTo
    )
    {
        DialogParameters<RelationshipAddDialog<FirstEntity, SecondEntity, EntityToAdd>> parameters = new()
        {
            { x => x.DialogLabel, dialogLabel },
            { x => x.GetSearchDisplayFunc, getSearchDisplayFunc },
            { x => x.SearchFunc, searchFunc },
            { x => x.EntityBeingAddedTo, entityBeingAddedTo },
        };
        DialogOptions options = new()
        {
            FullWidth = true
        };
        IDialogReference reference =
            await dialogService.ShowAsync<RelationshipAddDialog<FirstEntity, SecondEntity, EntityToAdd>>(title,
                parameters, options);
        return await reference.Result;
    }
}