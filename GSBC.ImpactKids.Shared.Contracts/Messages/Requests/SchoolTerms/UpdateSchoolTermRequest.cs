using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base.Interfaces;

namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.SchoolTerms;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class UpdateSchoolTermRequest : ReadRequestBase
{
    public override string              Id   { get; set; } = null!;
    public          DeltaUpdate<string> Name { get; set; } = new();

    public DeltaUpdate<DateTime> StartDate { get; set; } = new();
    public DeltaUpdate<DateTime> EndDate   { get; set; } = new();
}