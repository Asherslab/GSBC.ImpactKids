using EasyAppDev.Blazor.Store.Core;

namespace GSBC.ImpactKids.WASM.Services.RefreshableStore;

public interface IRefreshableStore<T> : IStore<EntityListState<T>>, IRefreshableStore;

public interface IRefreshableStore
{
    Task RefreshAll();
    Task RefreshEvent();
};

/// <summary>
/// Failure reasons a page is expected to tell apart, rather than render as prose.
/// </summary>
public static class RefreshableStoreErrors
{
    /// <summary>
    /// The wall display is not enrolled, or was enrolled on a key that has since been
    /// rotated. Distinguished from every other failure because it is the one with a remedy:
    /// open the setup link on the screen again. Everything else is waited out.
    /// </summary>
    public const string NotEnrolled = "NOT_ENROLLED";
}