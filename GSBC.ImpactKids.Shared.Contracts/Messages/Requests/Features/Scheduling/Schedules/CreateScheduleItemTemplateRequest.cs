namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.Schedules;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class CreateScheduleItemTemplateRequest
{
    public string?   Name        { get; set; }
    public int       Minutes     { get; set; } = 10;
    public TimeSpan? StartsAt    { get; set; }
    public bool      NeedsFillIn { get; set; }
}
