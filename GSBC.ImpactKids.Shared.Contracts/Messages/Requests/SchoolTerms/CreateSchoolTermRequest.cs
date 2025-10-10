namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.SchoolTerms;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class CreateSchoolTermRequest
{
    public string Name { get; set; } = null!;

    public DateTime StartDate { get; set; } = DateTime.Now;
    public DateTime EndDate   { get; set; } = DateTime.Now;
}