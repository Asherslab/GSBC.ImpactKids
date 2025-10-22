using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.Web.Components.Base;
using GSBC.ImpactKids.Web.Extensions;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.Web.Components.Pages.Users;

public partial class Users : EventListeningComponent
{
    [SupplyParameterFromQuery]
    public string? Search { get; set; }
    
    private ICollection<User>? _users;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();


        await RefreshUsers();
        await SubscribeToEvent(User.BuildSubscription(), RefreshUsers);
    }
    
    private async Task RefreshUsers()
    {
        BasicReadMultipleResponse<User>? response = await UsersService.ReadMultiple(
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

        _users = response.Entities;
        StateHasChanged();
    }
    
    private async Task OnSearch(string text)
    {
        Search = text;
        if (string.IsNullOrWhiteSpace(Search))
            Search = null;
        SetQueryParameters();
        await RefreshUsers();
    }
    
    private void SetQueryParameters()
    {
        Navigation.NavigateTo(GetQueryParameters());
    }

    private string GetQueryParameters()
    {
        return Navigation.GetUriWithQueryParameters(new Dictionary<string, object?>
        {
            [nameof(Search)] = Search
        });
    }

    private async Task ToggleUser(User user)
    {
        BasicReadRequest request = new()
        {
            Guid = user.Id
        };

        BasicResponse? response = await UsersService.ToggleEnabled(request);
        
        if (response.HasErrorOrNull())
        {
            Snackbar.AddErrorResponse(response);
        }
    }

    private async Task DeleteUser(User user)
    {
        bool? result = await DialogService.ShowMessageBox(
            "Warning",
            "Deleting can not be undone!",
            yesText: "Delete!", cancelText: "Cancel");

        if (result == null)
            return;

        BasicReadRequest request = new()
        {
            Guid = user.Id
        };

        BasicResponse? response = await UsersService.Delete(request);
        
        if (response.HasErrorOrNull())
        {
            Snackbar.AddErrorResponse(response);
        }
    }
}