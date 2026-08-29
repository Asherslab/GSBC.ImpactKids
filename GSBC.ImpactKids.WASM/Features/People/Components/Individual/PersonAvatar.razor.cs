using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace GSBC.ImpactKids.WASM.Features.People.Components.Individual;

/// <summary>
/// A person's face, wherever one appears.
///
/// <b>The initial is the component and the photo is an enhancement painted over it.</b> That is the
/// whole design: there is no "has photo" branch that renders something different, so every way a
/// photo can fail — a 404, a dropped connection, an offline phone, an object the store has lost —
/// lands on the same path and the card looks exactly as it did before photos existed.
/// </summary>
public partial class PersonAvatar
{
    /// <summary>Null renders the plain initial avatar, which is also what a person with no photo gets.</summary>
    [Parameter]
    public Person? Person { get; set; }

    [Parameter]
    public bool Rounded { get; set; } = true;

    [Parameter]
    public Color Color { get; set; } = Color.Default;

    [Parameter]
    public Size Size { get; set; } = Size.Medium;

    [Parameter]
    public string? Class { get; set; }

    private bool _loaded;
    private bool _failed;

    private string Initial => Person?.FirstName is { Length: > 0 } name
        ? name[0].ToString()
        : "N";

    private Person? _renderedFor;

    /// <summary>
    /// Resets the load state when the avatar is pointed at a different photo. Without this a
    /// recycled card - the person lists reuse these as rows change - would keep the previous
    /// person's "failed" flag and refuse to show a photo that is perfectly fine.
    /// </summary>
    protected override void OnParametersSet()
    {
        if (_renderedFor?.Id == Person?.Id && _renderedFor?.PhotoVersion == Person?.PhotoVersion)
            return;

        _renderedFor = Person;
        _loaded      = false;
        _failed      = false;
    }

    private void OnPhotoLoaded() => _loaded = true;

    private void OnPhotoError() => _failed = true;
}
