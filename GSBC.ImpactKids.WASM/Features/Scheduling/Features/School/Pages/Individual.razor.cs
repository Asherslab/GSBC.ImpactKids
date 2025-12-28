using System.Collections.Immutable;
using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.School;
using GSBC.ImpactKids.WASM.Extensions;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.Scheduling.Features.School.Pages;

public partial class Individual : ComponentBase
{
    [Parameter]
    public Guid? Id { get; set; }

    private AsyncData<SchoolTerm> _schoolTerm = AsyncData<SchoolTerm>.NotAsked();

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
        AsyncData<ImmutableList<SchoolTerm>> schoolTerms = SchoolTermsStore.GetState().Entities;

        if (!schoolTerms.HasData)
        {
            _schoolTerm = _schoolTerm.CopyStatus(schoolTerms);
            StateHasChanged();
            return;
        }

        SchoolTerm? schoolTerm = schoolTerms.Data!
            .FirstOrDefault(x => x.Id == Id);

        _schoolTerm = schoolTerm == null
            ? _schoolTerm.ToFailure("Failed to find School Term")
            : _schoolTerm.ToSuccess(schoolTerm);
        StateHasChanged();
    }
}