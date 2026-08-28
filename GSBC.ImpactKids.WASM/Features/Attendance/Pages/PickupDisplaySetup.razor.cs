using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Attendance;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Features.Attendance;

namespace GSBC.ImpactKids.WASM.Features.Attendance.Pages;

/// <summary>
/// The admin end of the pickup wall's enrolment key. Reads when the current key was minted
/// and by whom, and mints a new one on request.
/// <para>
/// The key comes back exactly once, from the rotation that created it - only a hash is
/// stored, so this page holds the only copy that will ever exist outside the TV's bookmark.
/// It is kept in a field and never round-tripped anywhere.
/// </para>
/// </summary>
public partial class PickupDisplaySetup
{
    private DateTime? _rotatedAt;
    private string?   _rotatedBy;
    private string?   _setupUrl;
    private string?   _error;
    private bool      _busy;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        await Refresh();
    }

    private async Task Refresh()
    {
        PickupDisplayKeyResponse response =
            await PickupDisplayKeyService.GetKeyInfo(new PickupDisplayKeyRequest());

        if (!response.Success)
        {
            _error = response.Error;
            return;
        }

        _error = null;
        _rotatedAt = response.RotatedAt;
        _rotatedBy = response.RotatedBy;
    }

    private async Task Rotate()
    {
        _busy = true;

        try
        {
            PickupDisplayKeyResponse response =
                await PickupDisplayKeyService.Rotate(new PickupDisplayKeyRequest());

            if (!response.Success || response.Key == null)
            {
                _error = response.Error ?? "Could not rotate the key";
                return;
            }

            _error = null;
            _rotatedAt = response.RotatedAt;
            _rotatedBy = response.RotatedBy;

            // The key rides the query string once, here, and is spent when the TV opens
            // this link - after that a cookie carries the screen. See
            // docs/modules/auth/sign-in.md.
            _setupUrl = $"{Navigation.BaseUri.TrimEnd('/')}/bff/display-login?key={Uri.EscapeDataString(response.Key)}";
        }
        finally
        {
            _busy = false;
        }
    }
}
