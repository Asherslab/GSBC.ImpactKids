using System.Collections.Immutable;
using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.School;
using GSBC.ImpactKids.WASM.Features.Scheduling.Features.Services.Pages;
using GSBC.ImpactKids.WASM.Services.RefreshableStore;

namespace GSBC.ImpactKids.WASM.Features.Scheduling.Features.Services;

public record MultipleServicesState(
    DateTime?                                                            Date,
    ServiceDisplayOptions                                                Display,
    Guid?                                                                ServiceType,
    AsyncData<ImmutableList<SchoolTerm>>                                 FilteredSchoolTerms
) : IInitialisableState<MultipleServicesState>
{
    public static MultipleServicesState Initial => new(
        DateTime.Now,
        ServiceDisplayOptions.Quarters,
        null,
        AsyncData<ImmutableList<SchoolTerm>>.NotAsked()
    );

    public MultipleServicesState SetDate(DateTime? date) => this with { Date = date };
    public MultipleServicesState SetDisplay(ServiceDisplayOptions display) => this with { Display = display };
    public MultipleServicesState SetServiceType(Guid? serviceType) => this with { ServiceType = serviceType };
}