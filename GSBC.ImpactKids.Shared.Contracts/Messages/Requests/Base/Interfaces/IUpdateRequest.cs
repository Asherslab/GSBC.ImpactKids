namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base.Interfaces;

public interface IUpdateRequest<in TEntity, out TRequest>
{
    static abstract TRequest FromEntity(TEntity entity);
}