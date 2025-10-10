namespace GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base.Interfaces;

public interface IExceptionableResponse
{
    public Exception? Exception { get; set; }

    public static virtual IExceptionableResponse? GetExceptionResponse(Exception ex) => null;
}