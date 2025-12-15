namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.School.SchoolTerms;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class CreateSchoolTermRequest
{
    public string Name { get; set; } = null!;
    
    public DateTime StartDate { get; set; } = DateTime.Now;
    public DateTime EndDate   { get; set; } = DateTime.Now;

    [ProtoIgnore]
    public DateTime LocalStartDate
    {
        get => StartDate.ToLocalTime();
        set => StartDate = value.ToUniversalTime();
    }

    [ProtoIgnore]
    public DateTime LocalEndDate
    {
        get => EndDate.ToLocalTime();
        set => EndDate = value.ToUniversalTime();
    }
}