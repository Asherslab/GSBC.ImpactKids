using GSBC.ImpactKids.WASM.Features.Scheduling.Features.Services.Pages;

namespace GSBC.ImpactKids.WASM.Features.Scheduling.Features.Services;

public record MultipleServicesState(
    DateTime?             Date,
    ServiceDisplayOptions Display,
    Guid?                 ServiceType
)
{
    public static MultipleServicesState Initial => new(DateTime.Now, ServiceDisplayOptions.Quarters, null);

    public MultipleServicesState SetDate(DateTime? date) => this with { Date = date };
    public MultipleServicesState SetDisplay(ServiceDisplayOptions display) => this with { Display = display };
    public MultipleServicesState SetServiceType(Guid? serviceType) => this with { ServiceType = serviceType };
}