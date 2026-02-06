using EasyAppDev.Blazor.Store.AsyncActions;

namespace GSBC.ImpactKids.WASM.Extensions;

public static class AsyncDataExtensions
{
    public static AsyncData<T> CopyStatus<T, TIn>(this AsyncData<T> copyTo, AsyncData<TIn> copyFrom)
    {
        if (copyFrom.IsNotAsked)
            return copyTo with { IsNotAsked = true };
        
        if (copyFrom.IsLoading)
            return copyTo.ToLoading();

        if (copyFrom.HasError)
            return copyTo.ToFailure(copyFrom.Error ?? "");

        return copyTo;
    }
}