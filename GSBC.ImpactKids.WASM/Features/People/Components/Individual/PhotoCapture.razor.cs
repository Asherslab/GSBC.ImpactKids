using System.Net.Http.Headers;
using System.Net.Http.Json;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

namespace GSBC.ImpactKids.WASM.Features.People.Components.Individual;

/// <summary>
/// Full-screen capture for one child: aim, shoot, confirm or retake.
///
/// <b>The live camera is the fast path and the file input is a first-class one</b>, not a
/// contingency bolted on later. `getUserMedia` missing, refused, or opening a stream that never
/// produces a frame are all ordinary runtime states here, and every one of them lands on
/// <c>&lt;input type="file" accept="image/*" capture="user"&gt;</c> with a plain line on screen
/// saying why. That matters because the iPhone confirmation happens after this is deployed rather
/// than before: built this way, a surprise in production degrades the tool instead of breaking it.
///
/// Nothing is written to the camera roll on either route, and there is no app switch between
/// children on the live path — the difference between three taps per child and a round trip through
/// the camera app each time.
/// </summary>
public partial class PhotoCapture : IAsyncDisposable
{
    [Parameter, EditorRequired]
    public Person? Person { get; set; }

    /// <summary>Raised with the new <c>PhotoVersion</c> once the upload has succeeded.</summary>
    [Parameter]
    public EventCallback<string> OnSaved { get; set; }

    [Parameter]
    public EventCallback OnClose { get; set; }

    [Inject]
    public required IJSRuntime Js { get; set; }

    [Inject]
    public required HttpClient Http { get; set; }

    private IJSObjectReference? _module;
    private ElementReference    _video;

    private bool    _liveCamera;
    private bool    _multipleCameras;
    private bool    _starting = true;
    private bool    _busy;
    private string? _preview;
    private string? _error;
    private string? _cameraMessage;

    /// <summary>
    /// A front camera is mirrored in the preview, because a leader aiming a phone at themselves or
    /// at a child beside them expects a mirror. The stored photo is not mirrored — see the JS.
    /// </summary>
    private bool Mirrored => _facingMode == "user";

    private string _facingMode = "user";

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        _module = await Js.InvokeAsync<IJSObjectReference>(
            "import", "./js/photoCapture.js");

        _multipleCameras = await _module.InvokeAsync<bool>("hasMultipleCameras");

        await StartCameraAsync();
    }

    private async Task StartCameraAsync()
    {
        if (_module == null) return;

        _starting = true;
        StateHasChanged();

        CameraResult result = await _module.InvokeAsync<CameraResult>("start", _video, _facingMode);

        _liveCamera = result.Ok;
        _cameraMessage = result.Ok ? null : MessageFor(result.Reason);
        _starting = false;

        StateHasChanged();
    }

    /// <summary>
    /// What the leader is told. Every one of these ends in the same place — the file input — so the
    /// wording is about not looking broken rather than about diagnosis.
    /// </summary>
    private static string MessageFor(string? reason) => reason switch
    {
        "denied"   => "Camera permission was refused, so we'll use your phone's camera app instead.",
        "nocamera" => "No camera was found, so choose a photo from your library instead.",
        _          => "Live camera unavailable, using your phone's camera app."
    };

    private async Task FlipCameraAsync()
    {
        _facingMode = _facingMode == "user" ? "environment" : "user";
        await StartCameraAsync();
    }

    private async Task TakePhotoAsync()
    {
        if (_module == null) return;

        _preview = await _module.InvokeAsync<string>("capture", _video);

        // The stream is released as soon as there is something to confirm, so the camera light goes
        // out while the leader decides rather than staying on through the whole review.
        await _module.InvokeVoidAsync("stop");
    }

    private async Task RetakeAsync()
    {
        _preview = null;
        await StartCameraAsync();
    }

    private async Task OnFilePickedAsync(InputFileChangeEventArgs e)
    {
        if (e.FileCount == 0) return;
        await CaptureFromFileAsync(e.File);
    }

    /// <summary>
    /// The picked file goes through the same crop-and-downscale as a live capture, so both routes
    /// produce byte-identical objects for the same image — which in turn means the same content
    /// hash, and one object in the store rather than two.
    /// </summary>
    private async Task CaptureFromFileAsync(IBrowserFile file)
    {
        if (_module == null) return;

        _busy = true;
        _error = null;
        StateHasChanged();

        try
        {
            await using Stream source = file.OpenReadStream(MaxUploadBytes);
            using MemoryStream buffer = new();
            await source.CopyToAsync(buffer);

            _preview = await _module.InvokeAsync<string>(
                "captureFromFileBytes", buffer.ToArray(), file.ContentType);

            await _module.InvokeVoidAsync("stop");
            _liveCamera = false;
        }
        catch (Exception ex)
        {
            _error = $"That image could not be read: {ex.Message}";
        }
        finally
        {
            _busy = false;
            StateHasChanged();
        }
    }

    private const long MaxUploadBytes = 20 * 1024 * 1024;

    private async Task SaveAsync()
    {
        if (_preview == null || Person == null) return;

        _busy  = true;
        _error = null;
        StateHasChanged();

        try
        {
            byte[] bytes = Convert.FromBase64String(_preview);

            using ByteArrayContent content = new(bytes);
            content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");

            HttpResponseMessage response = await Http.PostAsync(
                $"api/people/{Person.Id}/photo", content);

            if (!response.IsSuccessStatusCode)
            {
                // The status and reason only. The body is not echoed here: in Development the
                // service answers with the developer exception page, which carries a full header
                // dump including the caller's own bearer token - and this string is rendered on
                // screen. The detail belongs in the service's log, where it already is.
                _error = $"Saving the photo failed ({(int)response.StatusCode} {response.ReasonPhrase}). "
                         + "Try again, and check the service log if it keeps failing.";
                return;
            }

            PhotoUploaded? uploaded = await response.Content.ReadFromJsonAsync<PhotoUploaded>();

            await OnSaved.InvokeAsync(uploaded?.PhotoVersion ?? "");
            await CloseAsync();
        }
        catch (Exception ex)
        {
            _error = $"Saving the photo failed: {ex.Message}";
        }
        finally
        {
            _busy = false;
            StateHasChanged();
        }
    }

    private async Task CloseAsync()
    {
        await StopCameraAsync();
        await OnClose.InvokeAsync();
    }

    private async Task StopCameraAsync()
    {
        if (_module == null) return;
        try
        {
            await _module.InvokeVoidAsync("stop");
        }
        catch (JSDisconnectedException)
        {
            // The circuit is gone, so the camera went with it.
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopCameraAsync();

        if (_module != null)
        {
            try
            {
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
            }
        }
    }

    private sealed record CameraResult(bool Ok, string? Reason);

    private sealed record PhotoUploaded(string PhotoVersion);
}
