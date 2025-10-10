namespace GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base.Interfaces;

public interface IReadResponse<T>
{
    public T? Entity { get; set; }
}