namespace GSBC.ImpactKids.WASM.Features.Calendar.Models;

public class CalendarEvent
{
    public required DateTime Date  { get; set; }
    public required string   Name  { get; set; }
    public          string?  Color { get; set; }
    public          string?  Href  { get; set; }
}