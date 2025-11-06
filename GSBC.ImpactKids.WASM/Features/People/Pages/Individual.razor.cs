using GSBC.ImpactKids.Shared.Contracts.Entities.Pagination;
using GSBC.ImpactKids.Shared.Contracts.Entities.People;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.People;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.WASM.Components.Base;
using GSBC.ImpactKids.WASM.Extensions;
using GSBC.ImpactKids.WASM.Features.People.Components.Individual;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.People.Pages;

public partial class Individual : EventListeningComponent
{
    [Parameter]
    public Guid Id { get; set; }

    private Guid?                _familyId;
    private Person?              _person;
    private ICollection<Person>? _familyMembers;
    private PersonOverview?      _personOverviewComponent;

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();
        _person = _familyMembers?.FirstOrDefault(x => x.Id == Id);

        if (_person == null)
        {
            await RefreshPersonFamilyId();

            // the individual person subscription could theoretically end up with a lot of subscriptions if a person doesn't move off the page to refresh them.
            // should consider doing unbinding for that, I guess, not sure about the round trip times for it though
            await Task.WhenAll(
                RefreshFamilyMembers(),
                _familyId != null
                    ? SubscribeToEvent(Person.BuildSubscription(familyId: _familyId), RefreshFamilyMembers)
                    : Task.CompletedTask
            );
        }
    }

    private CancellationTokenSource _refreshPersonTokenSource = new();
    private async Task RefreshPersonFamilyId()
    {
        await _refreshPersonTokenSource.CancelAsync();
        _refreshPersonTokenSource = new CancellationTokenSource();
        
        BasicReadResponse<Person>? response = await PeopleService.Read(
            new BasicReadRequest
            {
                Guid = Id
            },
            _refreshPersonTokenSource.Token
        );

        _familyId = response?.Entity?.FamilyId;
        StateHasChanged();

        if (response.HasErrorOrNull())
        {
            Snackbar.AddErrorResponse(response);
        }
    }

    private CancellationTokenSource _refreshFamilyMembersTokenSource = new();
    private async Task RefreshFamilyMembers()
    {
        if (_familyId == null)
            return;
        
        await _refreshFamilyMembersTokenSource.CancelAsync();
        _refreshFamilyMembersTokenSource = new CancellationTokenSource();
        
        BasicReadMultipleResponse<Person>? response = await PeopleService.ReadMultiple(
            new PeopleRequest
            {
                FamilyId = _familyId,
                Pagination = PaginationRequest.All()
            }
        );

        _person = response?.Entities.FirstOrDefault(x => x.Id == Id);
        _personOverviewComponent?.PersonUpdated();
        _familyMembers = response?.Entities;
        StateHasChanged();

        if (response.HasErrorOrNull())
        {
            Snackbar.AddErrorResponse(response);
        }
    }
}