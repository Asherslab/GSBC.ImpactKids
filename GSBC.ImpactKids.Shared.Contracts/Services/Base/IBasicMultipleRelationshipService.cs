namespace GSBC.ImpactKids.Shared.Contracts.Services.Base;

[SubService]
public interface IBasicMultipleRelationshipService<FirstEntity, SecondEntity>
{
    Task<BasicResponse> CreateRelationship(
        BasicMultipleRelationshipRequest<FirstEntity, SecondEntity> request,
        CallContext                                                 context = default
    );

    Task<BasicResponse> DeleteRelationship(
        BasicMultipleRelationshipRequest<FirstEntity, SecondEntity> request,
        CallContext                                                 context = default
    );
}