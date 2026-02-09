using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scripture;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scripture.Memorisation;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scripture.Memorisation.MemoryVerses;
using GSBC.ImpactKids.Shared.Contracts.Services.Base;

namespace GSBC.ImpactKids.Shared.Contracts.Services.Features.Scripture.Memorisation;

[Service("gRPC/GSBC.ImpactKids.MemoryVerses")]
public interface IMemoryVersesService
    : IBasicReadMultipleService<MemoryVerse>,
        ICreateService<CreateMemoryVerseRequest>,
        IUpdateService<UpdateMemoryVerseRequest>,
        IBasicDeleteService<MemoryVerse>
{
    Task<BasicReadResponse<MemoryVerse>> Read(
        BasicReadRequest request,
        CallContext      context = default
    );
}

[Service("gRPC/GSBC.ImpactKids.MemoryVerses.Services")]
public interface IMemoryVersesServicesRelationshipService
    : IBasicMultipleRelationshipService<MemoryVerse, Service>;
    
    
[Service("gRPC/GSBC.ImpactKids.MemoryVerses.BibleVerses")]
public interface IMemoryVersesBibleVersesRelationshipService
    : IBasicMultipleRelationshipService<MemoryVerse, BibleVerse>;