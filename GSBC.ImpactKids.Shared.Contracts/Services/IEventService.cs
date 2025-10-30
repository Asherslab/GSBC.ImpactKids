using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Events;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses;

namespace GSBC.ImpactKids.Shared.Contracts.Services;

[Service("GSBC.ImpactKids.Event")]
public interface IEventService
{
    public IAsyncEnumerable<EventResponse> Stream(EventStreamRequest request, CallContext context = default);
    public Task<BasicReadResponse<Guid>>   Bind(EventBindRequest     request, CallContext context = default);
    public Task<BasicResponse>             Unbind(EventUnbindRequest request, CallContext context = default);

    // public Task<BasicResponse>             UnbindAll(EventUnbindAllRequest request, CallContext context = default);
}