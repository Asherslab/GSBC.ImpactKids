using System.Collections.Immutable;
using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.School;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.School.SchoolTerms;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.WASM.Components.Common.Inputs;
using GSBC.ImpactKids.WASM.Extensions;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.Scheduling.Features.School.Components.Individual;

public partial class SchoolTermDetails : ComponentBase
{
    [Parameter]
    public Guid? Id { get; set; }

    [Parameter]
    public ModificationState State { get; set; }

    private          AsyncData<SchoolTerm>   _schoolTerm    = AsyncData<SchoolTerm>.NotAsked();
    private readonly CreateSchoolTermRequest _createRequest = new();
    private          UpdateSchoolTermRequest _updateRequest = new();

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        SchoolTermsStore.Subscribe(_ => RetrieveSchoolTerm());

        await Task.WhenAll(
            SchoolTermsStore.RefreshAll()
        );
    }

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();

        RetrieveSchoolTerm();
    }

    private void RetrieveSchoolTerm()
    {
        if (State == ModificationState.Creating)
            return;

        AsyncData<ImmutableList<SchoolTerm>> schoolTerms = SchoolTermsStore.GetState().Entities;

        if (!schoolTerms.HasData)
        {
            _schoolTerm = _schoolTerm.CopyStatus(schoolTerms);
            StateHasChanged();
            return;
        }

        SchoolTerm? schoolTerm = schoolTerms.Data!
            .FirstOrDefault(x => x.Id == Id);

        if (schoolTerm == null)
        {
            _schoolTerm = _schoolTerm.ToFailure("Failed to find School Term");
            _updateRequest = new UpdateSchoolTermRequest();
            StateHasChanged();
            return;
        }

        _schoolTerm = _schoolTerm.ToSuccess(schoolTerm);

        _updateRequest = new UpdateSchoolTermRequest
        {
            Guid = schoolTerm.Id,
        };

        _updateRequest.Name.SetInitialValue(schoolTerm.Name);
        _updateRequest.LocalStartDate.SetInitialValue(schoolTerm.LocalStartDate);
        _updateRequest.LocalEndDate.SetInitialValue(schoolTerm.LocalEndDate);

        StateHasChanged();
    }
 
    public async Task<bool> CreateSchoolTerm()
    {
        _schoolTerm = _schoolTerm.ToLoading();
        StateHasChanged();
        BasicResponse resp = await SchoolTermsService.Create(_createRequest);

        if (resp.HasErrorOrNull())
        {
            RetrieveSchoolTerm();
            Snackbar.AddErrorResponse(resp);
            return false;
        }

        return true;
    }

    public async Task<bool> UpdateSchoolTerm()
    {
        if (_updateRequest.Guid == Guid.Empty)
            return false;

        _schoolTerm = _schoolTerm.ToLoading();
        StateHasChanged();
        BasicResponse resp = await SchoolTermsService.Update(_updateRequest);

        if (resp.HasErrorOrNull())
        {
            RetrieveSchoolTerm();
            Snackbar.AddErrorResponse(resp);
            return false;
        }

        return true;
    }

    public async Task DeleteSchoolTerm()
    {
        if (_schoolTerm.Data == null)
            return;
        Guid id = _schoolTerm.Data.Id;

        bool? result = await DialogService.ShowMessageBox(
            "Warning",
            "Deleting can not be undone!",
            yesText: "Delete!", cancelText: "Cancel");

        if (result == null)
            return;

        _schoolTerm = _schoolTerm.ToLoading();
        StateHasChanged();
        BasicReadRequest request = new() { Guid = id };
        BasicResponse    resp    = await SchoolTermsService.Delete(request);

        if (!resp.HasErrorOrNull())
            return;

        RetrieveSchoolTerm();
        Snackbar.AddErrorResponse(resp);
    }
}