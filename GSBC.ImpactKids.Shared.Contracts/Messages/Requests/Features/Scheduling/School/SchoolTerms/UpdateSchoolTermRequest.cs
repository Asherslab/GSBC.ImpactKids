using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.School;

namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.School.SchoolTerms;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class UpdateSchoolTermRequest : ReadRequestBase, IUpdateRequest<SchoolTerm, UpdateSchoolTermRequest>
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

    public static UpdateSchoolTermRequest FromEntity(SchoolTerm entity)
    {
        UpdateSchoolTermRequest request = new()
        {
            Guid = entity.Id,
        };

        request.Name.SetInitialValue(entity.Name);
        request.LocalStartDate.SetInitialValue(entity.LocalStartDate);
        request.LocalEndDate.SetInitialValue(entity.LocalEndDate);

        return request;
    }
}