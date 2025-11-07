namespace GSBC.ImpactKids.WASM.Services;

public class MetabaseConfig
{
    public required List<DashboardMapping> DashboardMappings { get; set; }
}

public class DashboardMapping
{
    public required string Label { get; set; }
    public required int    Id   { get; set; }
    public required string Type { get; set; }
}