using GSBC.ImpactKids.WASM.Services.RefreshableStore;

namespace GSBC.ImpactKids.WASM.Features.Attendance;

public record AttendanceToolState(
    string FilterKey
) : IInitialisableState<AttendanceToolState>
{
    public static AttendanceToolState Initial => new(
        "All"
    );

    public AttendanceToolState SetFilterKey(string filterKey) => this with { FilterKey = filterKey };
}