namespace GSBC.ImpactKids.Web.Services;

public class MetabaseConfig
{
    public required string                  Secret            { get; set; }
    public required Dictionary<string, int> DashboardMappings { get; set; }
}