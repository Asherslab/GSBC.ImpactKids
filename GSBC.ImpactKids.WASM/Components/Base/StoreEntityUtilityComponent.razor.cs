using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.Shared.Contracts.Services.Base;
using GSBC.ImpactKids.WASM.Extensions;

namespace GSBC.ImpactKids.WASM.Components.Base;

public partial class StoreEntityUtilityComponent<T> where T : notnull
{
    public async Task<DeleteResult> DeleteWithDialog<T>(
        IBasicDeleteService<T> service,
        Guid?                  id,
        Action?                loadingFunc = null,
        Action?                onError     = null
    )
    {
        if (id == null)
            return DeleteResult.NullId;

        bool? result = await DialogService.ShowMessageBox(
            "Warning",
            "Deleting can not be undone!",
            yesText: "Delete!", cancelText: "Cancel");

        if (result == null)
            return DeleteResult.Cancelled;

        loadingFunc?.Invoke();
        StateHasChanged();

        BasicReadRequest request = new() { Guid = id.Value };
        BasicResponse    resp    = await service.BasicDelete(request);

        if (!resp.HasErrorOrNull())
            return DeleteResult.Success;

        Snackbar.AddErrorResponse(resp);
        onError?.Invoke();
        return DeleteResult.Errored;
    }
}