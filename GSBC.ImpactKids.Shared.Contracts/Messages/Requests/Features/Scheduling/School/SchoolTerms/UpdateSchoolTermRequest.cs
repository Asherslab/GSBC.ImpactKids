using GSBC.ImpactKids.Shared.Contracts.Entities;

namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.School.SchoolTerms;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class UpdateSchoolTermRequest : ReadRequestBase
{
    public UpdateSchoolTermRequest()
    {
        LocalStartDate = new DelegatingDeltaUpdate<DateTime>(
            StartDate,
            getter: x => x.ToLocalTime(),
            setter: x => x.ToUniversalTime()
        );
        LocalEndDate = new DelegatingDeltaUpdate<DateTime>(
            EndDate,
            getter: x => x.ToLocalTime(),
            setter: x => x.ToUniversalTime()
        );
    }

    public override string              Id   { get; set; } = null!;
    public          DeltaUpdate<string> Name { get; set; } = new();

    public DeltaUpdate<DateTime> StartDate { get; set; } = new();
    public DeltaUpdate<DateTime> EndDate   { get; set; } = new();

    [ProtoIgnore]
    public DelegatingDeltaUpdate<DateTime> LocalStartDate { get; set; }

    [ProtoIgnore]
    public DelegatingDeltaUpdate<DateTime> LocalEndDate { get; set; }
}