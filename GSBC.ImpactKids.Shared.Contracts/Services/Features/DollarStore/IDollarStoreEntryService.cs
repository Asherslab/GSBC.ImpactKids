using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.DollarStoreEntries;
using GSBC.ImpactKids.Shared.Contracts.Services.Base;

namespace GSBC.ImpactKids.Shared.Contracts.Services.Features.DollarStore;

[Service("GSBC.ImpactKids.DollarStoreEntries")]
public interface IDollarStoreEntryService
    : IBasicReadMultipleService<DollarStoreEntry>,
        ICreateService<CreateDollarStoreEntryRequest>,
        IUpdateService<UpdateDollarStoreEntryRequest>,
        IBasicDeleteService<DollarStoreEntry>
{
    Task<BasicReadResponse<DollarStoreEntry>> Read(
        BasicReadRequest request,
        CallContext      context = default
    );
}