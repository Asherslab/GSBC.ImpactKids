using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scripture.Memorisation;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scripture.Memorisation.MemorisationEntries;
using GSBC.ImpactKids.Shared.Contracts.Services.Base;

namespace GSBC.ImpactKids.Shared.Contracts.Services.Features.Scripture.Memorisation;

[Service("gRPC/GSBC.ImpactKids.MemorisationEntries")]
public interface IMemorisationEntriesService
    : IBasicReadMultipleService<MemorisationEntry>,
        ICreateService<CreateMemorisationEntryRequest>,
        IUpdateService<UpdateMemorisationEntryRequest>;