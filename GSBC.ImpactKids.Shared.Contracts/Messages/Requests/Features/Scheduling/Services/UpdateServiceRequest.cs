using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.School;

namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.Services;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class UpdateServiceRequest : ReadRequestBase
{
    public override string Id { get; set; } = null!;

    public DeltaUpdate<string?>  Name { get; set; } = new();
    public DeltaUpdate<DateTime> Date { get; set; } = new();

    // Used by frontend only.
    [ProtoIgnore]
    public SchoolTerm? SchoolTerm
    {
        get;
        set
        {
            field = value;
            if (SchoolTermId.Value != value?.Id)
                SchoolTermId.Value = value?.Id;
        }
    }

    public DeltaUpdate<Guid?> SchoolTermId { get; set; } = new();

    // Used by frontend only.
    [ProtoIgnore]
    public ServiceType? ServiceType
    {
        get;
        set
        {
            field = value;
            if (ServiceTypeId.Value != value?.Id)
                ServiceTypeId.Value = value?.Id;
        }
    }

    public DeltaUpdate<Guid?> ServiceTypeId { get; set; } = new();
}