using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.DollarStoreEntries;

namespace GSBC.ImpactKids.Shared.Contracts.Services;

[Service("GSBC.ImpactKids.DollarStoreEntries")]
public interface IDollarStoreEntryService
{
    Task<BasicResponse?> Create(
        CreateDollarStoreEntryRequest request,
        CallContext                   context = default
    );

    Task<BasicReadResponse<DollarStoreEntry>?> Read(
        BasicReadRequest request,
        CallContext      context = default
    );

    Task<BasicReadMultipleResponse<DollarStoreEntry>?> ReadMultiple(
        DollarStoreEntriesRequest request,
        CallContext               context = default
    );

    Task<BasicResponse?> Update(
        UpdateDollarStoreEntryRequest request,
        CallContext                   context = default
    );

    Task<BasicResponse?> Delete(
        BasicReadRequest request,
        CallContext      context = default
    );
}