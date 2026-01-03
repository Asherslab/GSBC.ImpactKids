using System.Collections.Immutable;
using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People.MedicalNotes;
using GSBC.ImpactKids.WASM.Components.Common;
using GSBC.ImpactKids.WASM.Components.Common.Inputs;
using GSBC.ImpactKids.WASM.Extensions;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace GSBC.ImpactKids.WASM.Features.People.Features.MedicalNotes.Components.Individual;

public partial class MedicalNoteDisplay
{
    [Parameter]
    public bool None { get; set; }

    [Parameter]
    public bool AllowUpdating { get; set; }

    [Parameter]
    public bool AllowDeleting { get; set; }

    private AsyncData<MedicalType> _medicalType = AsyncData<MedicalType>.NotAsked();

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        await Task.WhenAll(
            EntityStore.RefreshAll(),
            MedicalTypesStore.RefreshAll()
        );
    }

    private string? _avatarDisplay;
    private Color   _avatarColor = Color.Default;
    private string  _displayText = "Medical not requested";

    protected override void OnRetrievedEntity()
    {
        AsyncData<ImmutableList<MedicalType>> medicalTypes = MedicalTypesStore.GetState().Entities;

        if (!medicalTypes.HasData)
        {
            _medicalType = _medicalType.CopyStatus(medicalTypes);
            StateHasChanged();
            return;
        }

        _avatarDisplay = "N";

        MedicalType? medicalType = medicalTypes.Data!
            .FirstOrDefault(x => x.Id == Entity.Data!.MedicalTypeId);

        _medicalType = medicalType == null
            ? _medicalType.ToFailure("Failed to find Medical Type")
            : _medicalType.ToSuccess(medicalType);

        _avatarDisplay = None
            ? "N"
            : medicalType != null
                ? medicalType.Label[0].ToString()
                : "O";

        _displayText = None
            ? "Medical not requested"
            : Entity.Data == null
                ? "Type"
                : medicalType?.Label ?? "Other";

        _avatarColor = None
            ? Color.Error
            : Entity.Data == null
                ? Color.Default
                : Entity.Data.Severe
                    ? Color.Error
                    : medicalType?.Label == "None"
                        ? Color.Success
                        : Color.Primary;
    }

    private async Task OnUpdate() =>
        await DetailsComponentDialog.Open<MedicalNoteDetails>(
            DialogService,
            "Update Medical Note",
            ModificationState.Updating,
            Id
        );

    private async Task OnDelete()
    {
        await DeleteWithDialog(
            MedicalNoteService,
            Entity.Data?.Id,
            () => Entity = Entity.ToLoading(),
            RetrieveEntity
        );
    }
}