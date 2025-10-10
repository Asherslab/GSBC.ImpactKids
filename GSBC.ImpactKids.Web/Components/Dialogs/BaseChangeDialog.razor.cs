using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base.Interfaces;
using GSBC.ImpactKids.Web.Extensions;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace GSBC.ImpactKids.Web.Components.Dialogs;

public partial class BaseChangeDialog<T> : ComponentBase
    where T : ISuccessResponse, IErrorResponse
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;
    
    [Parameter]
    public required RenderFragment ChildContent { get; set; }
    
    [Parameter]
    public required EventCallback OnSubmit { get; set; }

    [Parameter]
    public T? Response { get; set; }
    
    private async Task Submit()
    {
        await OnSubmit.InvokeAsync();

        if (Response.HasErrorOrNull())
        {
            Snackbar.AddErrorResponse(Response);
            return;
        }

        MudDialog.Close(DialogResult.Ok(true));
    }

    private void Cancel() => MudDialog.Cancel();
}