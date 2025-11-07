using GSBC.ImpactKids.Shared.Contracts.Entities.People;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.People;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.WASM.Components.Dialogs.Create;
using GSBC.ImpactKids.WASM.Extensions;

namespace GSBC.ImpactKids.WASM.Features.People.Pages;

public partial class Multiple
{
    public string? Search { get; set; }

    private ICollection<Person>? _people;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        
        await RefreshPeople();
        await SubscribeToEvent(Person.BuildSubscription(), RefreshPeople);
    }

    private CancellationTokenSource _refreshPeopleTokenSource = new();
    private async Task RefreshPeople()
    {
        await _refreshPeopleTokenSource.CancelAsync();
        _refreshPeopleTokenSource = new CancellationTokenSource();
        
        BasicReadMultipleResponse<Person>? response = await PersonService.ReadMultiple(
            new PeopleRequest
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
        await PersonService.SyncWithElvanto();
    }

    private async Task CreatePerson()
    {
        await DialogService.ShowAsync<CreatePersonDialog>("Create Person");
    }
}