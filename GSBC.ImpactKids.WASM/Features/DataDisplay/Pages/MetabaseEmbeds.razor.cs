using System.Collections.Immutable;
using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.DataDisplay;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.DataDisplay.MetabaseEmbeds;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.WASM.Components.Base;
using GSBC.ImpactKids.WASM.Components.Common;
using GSBC.ImpactKids.WASM.Components.Common.Inputs;
using GSBC.ImpactKids.WASM.Extensions;
using GSBC.ImpactKids.WASM.Features.DataDisplay.Components;
using GSBC.ImpactKids.WASM.Features.Scheduling.Features.Schedules.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace GSBC.ImpactKids.WASM.Features.DataDisplay.Pages;

public partial class MetabaseEmbeds
{
    private AsyncData<ImmutableList<MetabaseEmbed>> _embeds = AsyncData<ImmutableList<MetabaseEmbed>>.NotAsked();

    /// <summary>What is on screen. Usually the store's list; just after a drop, a local copy with the drop applied.</summary>
    private ImmutableList<MetabaseEmbed> _shown = [];

    private ImmutableList<MetabaseMenuEntry> _menu = [];

    private ElementReference                     _root;
    private DotNetObjectReference<MetabaseEmbeds>? _self;
    private IJSObjectReference?                  _module;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        HandleSubscriptionDisposal(MetabaseEmbedsStore, RetrieveEmbeds);
        RetrieveEmbeds();

        await Task.WhenAll(MetabaseEmbedsStore.RefreshAll());
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        _self   ??= DotNetObjectReference.Create(this);
        _module ??= await Js.InvokeAsync<IJSObjectReference>("import", "./js/analyticsSortable.js");

        // Every render: folders come and go, and attach skips the lists it already has.
        await _module.InvokeVoidAsync("attach", _root, _self);
    }

    private void RetrieveEmbeds()
    {
        _embeds = MetabaseEmbedsStore.GetState().Entities;

        if (_embeds.HasData)
            Show(_embeds.Data!);
        else
            StateHasChanged();
    }

    private void Show(ImmutableList<MetabaseEmbed> embeds)
    {
        _shown = embeds;
        _menu = MetabaseMenu.Build(embeds);
        StateHasChanged();
    }

    /// <summary>A row dropped into a folder, or at the top of the menu when the folder is null.</summary>
    [JSInvokable]
    public async Task OnEmbedDropped(string embedId, string? parentId, int position)
    {
        Guid  id     = Guid.Parse(embedId);
        Guid? parent = Guid.TryParse(parentId, out Guid parsed) ? parsed : null;

        Show(MetabaseMenu.Move(_shown, id, parent, position));

        BasicResponse response = await MetabaseService.Move(new MoveMetabaseEmbedRequest
        {
            Id = id,
            ParentId = parent,
            Position = position
        });

        if (Snackbar.AddErrorResponse(response))
            RetrieveEmbeds();

        await MetabaseEmbedsStore.RefreshEvent();
    }

    private async Task CreateEmbed() =>
        await DetailsComponentDialog.Open<MetabaseEmbedDetails>(DialogService, "Add Analytics Page",
            ModificationState.Creating);

    private async Task UpdateEmbed(MetabaseEmbed embed) =>
        await DetailsComponentDialog.Open<MetabaseEmbedDetails>(DialogService, "Update Analytics Page",
            ModificationState.Updating, embed.Id);

    private async Task AddFolder()
    {
        string? name = await TextPromptDialog.Ask(DialogService, "Add a folder", "Folder name",
            placeholder: "Memorisation, Attendance", submitText: "Add");

        if (name == null)
            return;

        BasicReadResponse<Guid?> response = await MetabaseService.Create(new CreateMetabaseEmbedRequest
        {
            Label = name,
            Type = MetabaseEmbedType.Folder
        });

        Snackbar.AddErrorResponse(response);
        await MetabaseEmbedsStore.RefreshEvent();
    }

    private async Task RenameFolder(MetabaseEmbed folder)
    {
        string? name = await TextPromptDialog.Ask(DialogService, "Rename folder", "Folder name", folder.Label);

        if (name == null || name == folder.Label)
            return;

        UpdateMetabaseEmbedRequest request = UpdateMetabaseEmbedRequest.FromEntity(folder);
        request.Label.Value = name;

        BasicResponse response = await MetabaseService.Update(request);

        Snackbar.AddErrorResponse(response);
        await MetabaseEmbedsStore.RefreshEvent();
    }

    private async Task DeleteEmbed(MetabaseEmbed embed)
    {
        DeleteResult result = await DeleteWithDialog(
            MetabaseService,
            embed.Id,
            () => _embeds = _embeds.ToLoading(),
            RetrieveEmbeds
        );

        if (result == DeleteResult.Success)
            await MetabaseEmbedsStore.RefreshEvent();
    }

    public override void Dispose()
    {
        _self?.Dispose();

        if (_module != null)
            _ = _module.DisposeAsync().AsTask();

        base.Dispose();
    }
}
