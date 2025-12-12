using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scripture.Memorisation;
using GSBC.ImpactKids.Shared.Contracts.Entities.Pagination;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.People;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scripture.Memorisation.MemorisationEntries;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.WASM.Components.Base;
using GSBC.ImpactKids.WASM.Extensions;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.People.Pages;

public partial class Individual : EventListeningComponent
{
    [Parameter]
    public Guid Id { get; set; }

    private Guid?                           _familyId;
    private Person?                         _person;
    private ICollection<Person>?            _familyMembers;
    private ICollection<MemorisationEntry>? _memorisationEntries;

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();
        bool personChanged = Id != _person?.Id;
        _person = _familyMembers?.FirstOrDefault(x => x.Id == Id);

        if (_person == null)
        {
            await RefreshPersonFamilyId();

            // some subscriptions here could build up if there's a lot of switching between people.
            await Task.WhenAll(
                RefreshFamilyMembers(),
                RefreshMemorisationEntries(),
                _familyId != null
                    ? SubscribeToEvent(Person.BuildSubscription(familyId: _familyId), RefreshFamilyMembers)
                    : Task.CompletedTask,
                _familyId != null
                    ? SubscribeToEvent(MemorisationEntry.BuildSubscription(personId: Id), RefreshMemorisationEntries)
                    : Task.CompletedTask
            );
        }
        else if (personChanged)
        {
            // some subscriptions here could build up if there's a lot of switching between people.
            await Task.WhenAll(
                RefreshMemorisationEntries(),
                _familyId != null
                    ? SubscribeToEvent(MemorisationEntry.BuildSubscription(personId: Id), RefreshMemorisationEntries)
                    : Task.CompletedTask
            );
        }
    }

    private CancellationTokenSource _refreshPersonTokenSource = new();

    private async Task RefreshPersonFamilyId()
    {
        await _refreshPersonTokenSource.CancelAsync();
        _refreshPersonTokenSource = new CancellationTokenSource();

        BasicReadResponse<Person>? response = await PersonService.Read(
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

    private CancellationTokenSource _refreshMemorisationEntriesTokenSource = new();

    private async Task RefreshMemorisationEntries()
    {
        _memorisationEntries = null;
        StateHasChanged();

        await _refreshMemorisationEntriesTokenSource.CancelAsync();
        _refreshMemorisationEntriesTokenSource = new CancellationTokenSource();

        BasicReadMultipleResponse<MemorisationEntry>? response = await MemorisationEntriesService.ReadMultiple(
            new MemorisationEntriesRequest
            {
                Pagination = PaginationRequest.All(),
                
                IncludeService = true,
                IncludeMemoryVerse = true,

                PersonId = Id,
                CurrentSchoolTerm = true
            },
            _refreshPersonTokenSource.Token
        );

        _memorisationEntries = response?.Entities;
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

        _familyMembers = null;
        _person = null;
        StateHasChanged();

        await _refreshFamilyMembersTokenSource.CancelAsync();
        _refreshFamilyMembersTokenSource = new CancellationTokenSource();

        BasicReadMultipleResponse<Person>? response = await PersonService.ReadMultiple(
            new PeopleRequest
            {
                FamilyId = _familyId,
                Pagination = PaginationRequest.All()
            }
        );

        _person = response?.Entities.FirstOrDefault(x => x.Id == Id);
        _familyMembers = response?.Entities;
        StateHasChanged();

        if (response.HasErrorOrNull())
        {
            Snackbar.AddErrorResponse(response);
        }
    }
    
    private async Task DeletePerson()
    {
        bool? result = await DialogService.ShowMessageBox(
            "Warning",
            "Deleting can not be undone!",
            yesText: "Delete!", cancelText: "Cancel");

        if (result == null || _person == null)
            return;

        BasicReadRequest request = new()
        {
            Guid = _person.Id
        };

        await Unbind(); // unbinds this component from events first, so that we don't get a refresh after deleting before navigating
        await PersonService.Delete(request);
        Navigation.NavigateTo("/People");
    }
}