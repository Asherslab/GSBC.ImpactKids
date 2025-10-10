using System.Diagnostics.CodeAnalysis;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base.Interfaces;
using MudBlazor;

namespace GSBC.ImpactKids.Web.Extensions;

public static class ResponseExtensions
{
    public static bool HasErrorOrNull([NotNullWhen(false)] this ISuccessResponse? successResponse)
    {
        return successResponse is not { Success: true };
    }

    public static bool AddErrorResponse(
        this                 ISnackbar       snackbar,
        [NotNullWhen(true)] IErrorResponse? errorResponse,
        Severity                             severity = Severity.Error
    )
    {
        if (errorResponse?.Error == null) return false;
        
        snackbar.Add(errorResponse.Error, severity);
        return true;
    }
}