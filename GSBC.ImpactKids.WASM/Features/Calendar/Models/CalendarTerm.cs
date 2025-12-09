namespace GSBC.ImpactKids.WASM.Features.Calendar.Models;

public class CalendarTerm
{
    public required DateTime StartDate { get; set; }
    public required DateTime EndDate   { get; set; }
    public required string   Name      { get; set; }
    public          string?  Color     { get; set; }
}