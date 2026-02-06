using System.Collections.Immutable;
using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.WASM.Extensions;

namespace GSBC.ImpactKids.WASM.Features.Authentication.Pages;

public partial class Multiple
{
    protected override async Task OnInitializedAsync()
    {
        SubscribeToSelector(s => s.Search, _ => UpdateFilteredUsers());
        UsersStore.Subscribe(_ => UpdateFilteredUsers());

        await Task.WhenAll(
            UsersStore.RefreshAll(),
            UpdateFilteredUsers()
        );
    }

    private Task UpdateFilteredUsers()
    {
        AsyncData<ImmutableList<User>> user = UsersStore.GetState().Entities;

        if (!user.HasData)
            return Update(s => s with { FilteredUsers = user });

        // string[]? searchStrings = State.Search?.Split(" ");
        return Update(s => s with
        {
            FilteredUsers = s.FilteredUsers.ToSuccess(
                user.Data!
                    .FuzzySearch(
                        query: State.Search,
                        threshold: 20,
                        orderByBest: true,
                        fields:
                        [
                            x => x.Name
                        ]
                    )
                    .Take(10)
                    .ToImmutableList()
            )
        });
    }

    private async Task OnSearch(string text)
    {
        await UpdateDebounced(s =>
            {
                string? nullableText = text;
                if (string.IsNullOrWhiteSpace(nullableText))
                    nullableText = null;
                return s.SetSearch(nullableText);
            },
            TimeSpan.FromSeconds(0.25).Milliseconds
        );
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
            yesText: "Delete!", cancelText: "Cancel"
        );

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