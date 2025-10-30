using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.WASM.Components.Dialogs.Create;
using GSBC.ImpactKids.WASM.Components.Dialogs.Update;
using GSBC.ImpactKids.WASM.Extensions;
using MudBlazor;

namespace GSBC.ImpactKids.WASM.Pages.People;

public partial class People
{
    public string? Search { get; set; }

    private ICollection<Person>? _people;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        
        await RefreshPeople();
        await SubscribeToEvent(Person.BuildSubscription(), RefreshPeople);
    }

    private async Task RefreshPeople()
    {
        BasicReadMultipleResponse<Person>? response = await PeopleService.ReadMultiple(
            new BasicReadMultipleRequest
            {
                SearchString = Search
            }
        );

        if (response.HasErrorOrNull())
        {
            Snackbar.AddErrorResponse(response);
            return;
        }

        _people = response.Entities;
        StateHasChanged();
    }

    private async Task OnSearch(string text)
    {
        Search = text;
        if (string.IsNullOrWhiteSpace(Search))
            Search = null;
        await RefreshPeople();
    }

    private async Task SyncElvantoPeople()
    {
        await PeopleService.SyncWithElvanto();
    }

    private async Task CreatePerson()
    {
        await DialogService.ShowAsync<CreatePersonDialog>("Create Person");
    }

    private async Task UpdatePerson(Person person)
    {
        DialogParameters<UpdatePersonDialog> parameters = new()
        {
            { x => x.Person, person }
        };

        await DialogService.ShowAsync<UpdatePersonDialog>("Update Person", parameters);
    }
    
    private async Task DeletePerson(Person person)
    {
        bool? result = await DialogService.ShowMessageBox(
            "Warning",
            "Deleting can not be undone!",
            yesText: "Delete!", cancelText: "Cancel");

        if (result == null)
            return;

        BasicReadRequest request = new()
        {
            Guid = person.Id
        };

        BasicResponse? response = await PeopleService.Delete(request);

        if (response.HasErrorOrNull())
        {
            Snackbar.AddErrorResponse(response);
        }
    }
}